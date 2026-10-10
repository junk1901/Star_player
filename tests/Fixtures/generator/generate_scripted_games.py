#!/usr/bin/env python3
"""
Generates the Phase 2 scripted-game fixtures and their EXPECTED results.

The expected results come from the scorer in this file, which is a separate implementation (different language, written
from docs/game-engine.md rather than from the C# reducer). The C# tests run the generated events through StarPlayer's
GameReducer and compare every number. The C# engine's output is never used as an expected value.

Rules implemented here (docs/game-engine.md, 2026-10-11 fix round): GIDP and the withheld RBI only for force / reverse-force double plays (OBR 9.02(a)(17), 9.04(b)(1)), a voided-run runner is left on base
(9.03(c)), a pitcher's earned runs are judged from his own entry (9.16(i)), the relief walk-count table (9.16(h)), the starting DH is not replaced before his first plate appearance (5.11(a)(2)); no DH is ever
sent to the mound. This scorer and the engine share an author: the hand-computed oracles in tests/StarPlayer.Simulation.Tests are the independent check.

Usage:  python generate_scripted_games.py <out_100_games.json> <out_diagnostic_games.json> <rules_profiles.json> [<out_c33_games_30.json>]
Only the standard library is used. Output is deterministic (fixed seeds).
"""
import json
import random
import sys

DEFENSE = ['catcher', 'first_base', 'second_base', 'third_base', 'shortstop', 'left_field', 'center_field', 'right_field']
INFIELD = ['first_base', 'second_base', 'third_base', 'shortstop']
OUTFIELD = ['left_field', 'center_field', 'right_field']
SLOT_POS_DH = ['center_field', 'shortstop', 'right_field', 'first_base', 'designated_hitter', 'left_field', 'third_base', 'second_base', 'catcher']
SLOT_POS_NODH = ['center_field', 'shortstop', 'right_field', 'first_base', 'pitcher', 'left_field', 'third_base', 'second_base', 'catcher']
OTHER = {'away': 'home', 'home': 'away'}
ERROR_CAUSES = ('error_extra_base', 'error_saved_out')


def co(pos, kind):
    return {'position': pos, 'kind': kind}


class Team:
    def __init__(self, prefix, rules, starter_is_dh):
        dh = rules['designated_hitter']
        positions = SLOT_POS_DH if dh else SLOT_POS_NODH
        self.lineup = []
        for i, pos in enumerate(positions):
            player = prefix + 'p' if pos == 'pitcher' else f'{prefix}{i + 1}'
            self.lineup.append({'order': i + 1, 'player': player, 'pos': pos})
        self.starter = f'{prefix}5' if (starter_is_dh and dh) else prefix + 'p'
        self.starting_dh = next((s['player'] for s in self.lineup if s['pos'] == 'designated_hitter'), None)
        self.pitcher = self.starter
        self.bench = [f'{prefix}b{i}' for i in range(1, 6)]
        self.removed = []
        self.appeared = []
        for p in [s['player'] for s in self.lineup] + [self.pitcher]:
            if p not in self.appeared:
                self.appeared.append(p)
        self.ever = [s['player'] for s in self.lineup]
        self.next = 0
        self.usages = []
        self.pitched = []

    def slot(self, order):
        return next(s for s in self.lineup if s['order'] == order)

    def slot_of(self, player):
        return next((s for s in self.lineup if s['player'] == player), None)

    def slot_at(self, pos):
        return next((s for s in self.lineup if s['pos'] == pos), None)

    def batter_slot(self):
        return self.slot(self.next % 9 + 1)

    def usage(self):
        return [u for u in self.usages if u['pitcher'] == self.pitcher][-1]


class Game:
    def __init__(self, gid, rules, away, home):
        self.id = gid
        self.rules = rules
        self.team = {'away': away, 'home': home}
        self.inning = 1
        self.half = 'top'
        self.outs = 0
        self.balls = 0
        self.strikes = 0
        self.bases = {1: None, 2: None, 3: None}
        self.runs = {'away': 0, 'home': 0}
        self.line = {'away': [0], 'home': []}
        self.stage = 'start'
        self.walk_override = None
        self.runner_key = 1
        self.plays = []
        self.stats = []
        self.events = []
        self.status = 'play'
        self.outcome = None
        self.eid = 0
        self.half_ended = False
        away.usages.append(self._usage(away.starter, 0, 0, 0))
        home.usages.append(self._usage(home.starter, 0, 0, 0))
        away.pitched.append(away.starter)
        home.pitched.append(home.starter)

    @staticmethod
    def _usage(pitcher, seq, lead, runners):
        return {'pitcher': pitcher, 'entry_seq': seq, 'lead': lead, 'runners': runners, 'outs': 0, 'bf': 0}

    @property
    def bat(self):
        return 'away' if self.half == 'top' else 'home'

    @property
    def fld(self):
        return OTHER[self.bat]

    def next_id(self):
        self.eid += 1
        return f'e{self.eid}'

    def defender(self, side, pos):
        t = self.team[side]
        if pos == 'pitcher':
            return t.pitcher
        s = t.slot_at(pos)
        return s['player'] if s else None

    def catcher(self, side):
        return self.team[side].slot_at('catcher')['player']

    def add(self, team, player, key, amount=1, partner=None):
        self.stats.append((team, player, key, amount, partner))

    def defense_complete(self, side):
        t = self.team[side]
        if self.rules['designated_hitter']:
            return True
        return t.slot_at('pitcher')['player'] == t.pitcher

    def runner_count(self):
        return sum(1 for b in (1, 2, 3) if self.bases[b])


# --------------------------------------------------------------------------------------------- the independent scorer

def apply_event(g, ev):
    """Applies an event dict to the Python game state exactly as docs/game-engine.md describes. Returns nothing."""
    g.events.append(ev)
    t = ev['type']
    if t == 'approach':
        g.stage = 'insequence'
    elif t in ('pitch', 'take'):
        apply_pitch(g, ev, t == 'take')
    elif t == 'in_play':
        apply_in_play(g, ev)
    elif t == 'runner_play':
        apply_runner_play(g, ev)
    elif t == 'substitution':
        apply_sub(g, ev)
    else:
        raise ValueError(t)


def count_pitch(g, ev_id):
    fld = g.fld
    p = g.team[fld].pitcher
    c = g.catcher(fld)
    g.add(fld, p, 'pitchpitches')
    g.add(fld, c, 'catpitches')
    g.add(fld, c, 'batterypitches', 1, p)


def apply_pitch(g, ev, is_take):
    count_pitch(g, ev['id'])
    outcome = ev['outcome']
    rules = g.rules
    balls, strikes = g.balls, g.strikes
    ending = None
    if outcome == 'ball':
        if balls + 1 >= rules['balls_for_walk']:
            ending = 'walk'
        else:
            balls += 1
    elif outcome in ('called_strike', 'swinging_strike'):
        if strikes + 1 >= rules['strikes_for_strikeout']:
            ending = 'strikeout'
        else:
            strikes += 1
    elif outcome == 'foul':
        if strikes < rules['strikes_for_strikeout'] - 1:
            strikes += 1
    elif outcome == 'foul_bunt':
        if strikes + 1 >= rules['strikes_for_strikeout']:
            ending = 'strikeout'
        else:
            strikes += 1
    elif outcome == 'hit_by_pitch':
        ending = 'hit_by_pitch'
    mishap = ev.get('mishap', 'none')
    dropped = ev.get('dropped_third', 'none')
    cause = mishap if mishap != 'none' else dropped
    explicit = ev.get('moves', [])
    fld, bat = g.fld, g.bat
    pitcher = g.team[fld].pitcher
    if ending is None:
        if mishap == 'none':
            g.balls, g.strikes = balls, strikes
            g.stage = 'decision' if is_take else 'insequence'
            return
        g.balls, g.strikes = balls, strikes
        g.stage = 'decision' if is_take else 'insequence'
        plans = plans_from_moves(g, explicit)
        finish(g, ev['id'], 'wild_pitch' if mishap == 'wild_pitch' else 'passed_ball', False, None, 'none', plans, [],
               wp=mishap == 'wild_pitch', pb=mishap == 'passed_ball')
        return
    slot = g.team[bat].batter_slot()
    charged = (g.walk_override or pitcher) if ending == 'walk' else pitcher
    runner = {'key': g.runner_key, 'player': slot['player'], 'resp': charged, 'order': slot['order']}
    reaches = ending in ('walk', 'hit_by_pitch') or dropped != 'none'
    plans = plans_from_moves(g, explicit, default_forced=reaches)
    result = ending
    if reaches:
        plans.append({'runner': runner, 'frm': 0, 'to': 1, 'cause': cause if dropped != 'none' else 'forced', 'out_kind': None, 'credits': []})
    else:
        credits = ev.get('strikeout_credits') or [co('catcher', 'putout')]
        plans.append({'runner': runner, 'frm': 0, 'to': 0, 'cause': 'play', 'out_kind': 'other', 'credits': credits})
    if dropped != 'none':
        result = 'strikeout_reached_base'
    finish(g, ev['id'], 'pa', True, result, 'none', plans, [], wp=cause == 'wild_pitch', pb=cause == 'passed_ball',
           batter=slot['player'], charged=charged, reaches=reaches or False, breaches=bool(reaches), bfirst=not reaches)


def plans_from_moves(g, moves, default_forced=False, require_all=False, default_to=None):
    plans = []
    for m in moves:
        if m['from'] == 0:
            continue
        r = g.bases[m['from']]
        plans.append({'runner': r, 'frm': m['from'], 'to': m['to'], 'cause': m.get('cause', 'play'), 'out_kind': m.get('out_kind'),
                      'credits': m.get('credits', []), 'seq': m.get('sequence'), 'out_at': m.get('out_at')})
    for b in (1, 2, 3):
        r = g.bases[b]
        if r is None or any(p['frm'] == b for p in plans):
            continue
        if default_to is not None:
            plans.append({'runner': r, 'frm': b, 'to': default_to, 'cause': 'play', 'out_kind': None, 'credits': []})
            continue
        forced = default_forced and all(g.bases[x] for x in range(1, b))
        plans.append({'runner': r, 'frm': b, 'to': b + 1 if forced else b, 'cause': 'forced' if forced else 'play', 'out_kind': None, 'credits': []})
    return plans


def apply_in_play(g, ev):
    count_pitch(g, ev['id'])
    bat = g.bat
    slot = g.team[bat].batter_slot()
    pitcher = g.team[g.fld].pitcher
    runner = {'key': g.runner_key, 'player': slot['player'], 'resp': pitcher, 'order': slot['order']}
    result = ev['result']
    moves = [m for m in ev.get('moves', []) if m['from'] != 0]
    batter_move = next((m for m in ev.get('moves', []) if m['from'] == 0), None)
    if result == 'home_run':
        plans = plans_from_moves(g, moves, default_to=4)
    else:
        plans = plans_from_moves(g, moves)
    dest = {'single': 1, 'double': 2, 'triple': 3, 'home_run': 4, 'fielders_choice': 1}
    if result in ('single', 'double', 'triple') and batter_move is not None and batter_move['to'] == 0:
        # the batter was safe on the hit and was put out AT a later base: a non-force tag out, evidence of reaching = the out base
        plans.append({'runner': runner, 'frm': 0, 'to': 0, 'cause': 'play', 'out_kind': batter_move.get('out_kind'), 'credits': batter_move.get('credits', []),
                      'seq': batter_move.get('sequence', ev.get('batter_sequence')), 'out_at': batter_move['out_at'], 'post_reach': True})
    elif result in dest:
        plans.append({'runner': runner, 'frm': 0, 'to': dest[result], 'cause': 'play', 'out_kind': None, 'credits': [],
                      'seq': ev.get('batter_sequence') if result == 'home_run' else None})
    elif result == 'reached_on_error':
        plans.append({'runner': runner, 'frm': 0, 'to': batter_move['to'] if batter_move else 1, 'cause': 'error_saved_out', 'out_kind': None, 'credits': []})
    else:
        plans.append({'runner': runner, 'frm': 0, 'to': 0, 'cause': 'play', 'out_kind': 'other', 'credits': ev.get('batter_credits', []),
                      'seq': ev.get('batter_sequence')})
    final = {'single': 'single', 'double': 'double', 'triple': 'triple', 'home_run': 'home_run', 'fielders_choice': 'fielders_choice',
             'reached_on_error': 'reached_on_error'}.get(result)
    if final is None:
        outs_on = sum(1 for p in plans if p['to'] == 0)
        clean_runs = sum(1 for p in plans if p['to'] == 4 and p['cause'] not in ERROR_CAUSES)
        if ev.get('sacrifice'):
            final = 'sacrifice_bunt'
        elif ev['batted'] == 'fly' and outs_on == 1 and g.outs < g.rules['outs_per_half_inning'] - 1 and clean_runs > 0:
            final = 'sacrifice_fly'
        else:
            final = 'out'
    finish(g, ev['id'], 'pa', True, final, ev['batted'], plans, ev.get('errors', []),
           batter=slot['player'], charged=pitcher, reaches=result != 'out',
           breaches=result != 'out' or ev['batted'] in ('ground', 'bunt'),
           bfirst=result == 'out' and ev['batted'] in ('fly', 'line', 'pop'))


def apply_runner_play(g, ev):
    plans = plans_from_moves(g, ev.get('moves', []))
    kind = ev['kind']
    if kind == 'steal':
        steals = [p for p in plans if p['cause'] == 'steal']
        pk = 'caught_stealing' if any(p['to'] == 0 for p in steals) else 'steal'
    elif kind == 'pickoff':
        pk = 'pickoff'
    elif kind == 'balk':
        pk = 'balk'
    else:
        pk = 'other'
    finish(g, ev['id'], pk, False, None, 'none', plans, ev.get('errors', []),
           pickoff=kind == 'pickoff', balk=kind == 'balk')


def is_recon_out(p):
    """An out only the earned-run replay sees: an error saved the runner, or the batter reached on an error / passed ball."""
    return p['to'] != 0 and (p['cause'] == 'error_saved_out' or (p['frm'] == 0 and p['cause'] == 'passed_ball'))


def resolve(g, plans, batter_reaches, batter_out_first):
    """Orders the outs and runs of a play, classifies each out from the base picture and says which runs count.

    Rule (general baseball principle, KBO text not certified): no run counts if the third out is made on the batter-runner or is a force
    out; if it is any other out only runs that crossed the plate before it count. A runner is forced only while the batter-runner is not out
    and every base behind him is occupied by a runner not yet put out, and he is out AT the base he was forced to."""
    limit = g.rules['outs_per_half_inning']
    term = [p for p in plans if p['to'] in (0, 4) or is_recon_out(p)]
    bp = next((p for p in plans if p['frm'] == 0), None)
    bfirst = bp is not None and bp in term and batter_out_first and bp['to'] == 0
    implicit = bfirst and bp.get('seq') is None
    seq = {}
    if any(p.get('seq') is not None for p in term):
        for p in term:
            seq[p['runner']['key']] = 0 if (implicit and p is bp) else p['seq']
    else:
        order = []
        if bfirst:
            order.append(bp)
        order += sorted([p for p in term if p['frm'] >= 1], key=lambda p: -p['frm'])
        if bp is not None and bp in term and not bfirst:
            order.append(bp)
        for i, p in enumerate(order):
            seq[p['runner']['key']] = i + 1
    cls = {}
    out_at = {}
    retired = set()
    batter_forces = batter_reaches
    for p in sorted(term, key=lambda q: seq[q['runner']['key']]):
        key = p['runner']['key']
        if p['frm'] == 0:
            if p.get('post_reach'):
                cls[key] = 'nonforce'
                out_at[key] = p['out_at']
                continue
            if p['to'] == 0 or is_recon_out(p):
                cls[key] = 'batter'
            if p['to'] == 0:
                batter_forces = False
            continue
        is_out = p['to'] == 0
        if not is_out and not is_recon_out(p):
            continue
        frm = p['frm']
        at = p.get('out_at') or frm + 1
        forced = batter_forces and all(g.bases[b] is not None and b not in retired for b in range(1, frm))
        cls[key] = 'force' if forced and at == frm + 1 else 'nonforce'
        out_at[key] = at
        if is_out:
            retired.add(frm)
    outs = g.outs
    third = None
    for p in sorted(term, key=lambda q: seq[q['runner']['key']]):
        if p['to'] == 0:
            outs += 1
            if outs == limit and third is None:
                third = p
    counted = set()
    for p in plans:
        if p['to'] != 4:
            continue
        key = p['runner']['key']
        if third is None or (cls[third['runner']['key']] == 'nonforce' and seq[key] < seq[third['runner']['key']]):
            counted.add(key)
    return {'seq': seq, 'cls': cls, 'out_at': out_at, 'counted': counted}


def grounded_into_force_dp(g, plans, res, batted, is_pa):
    """OBR definitions (DOUBLE PLAY) with 9.02(a)(17) / 9.04(b)(1): two outs on a ground ball are a force double play (both putouts force plays) or a reverse-force
    double play (the first out is a force play, the second is made on a runner whose force the first out removed). The batter-runner put out on a ground ball is
    himself a force play. g.bases is still the base picture at the start of the play."""
    if not is_pa or batted != 'ground':
        return False
    outs = sorted([p for p in plans if p['to'] == 0], key=lambda p: res['seq'][p['runner']['key']])
    if len(outs) < 2:
        return False

    def cls(p):
        return res['cls'].get(p['runner']['key'])

    def is_force(p):
        return cls(p) == 'force' or (p['frm'] == 0 and cls(p) == 'batter')

    def is_reverse(p, first):
        if p['frm'] < 1 or cls(p) != 'nonforce':
            return False
        was_forced = all(g.bases[b] is not None for b in range(1, p['frm']))
        return was_forced and first['frm'] < p['frm']

    return is_force(outs[0]) and (is_force(outs[1]) or is_reverse(outs[1], outs[0]))


def settle(scorers, stayers, seniority):
    """OBR 9.16(g) Comment: a run is charged to the oldest responsibility slot among the scoring runners and the runners still on base; a stayer keeps his own slot when it is still free."""
    slots = sorted([r['resp'] for r in scorers + stayers], key=seniority)
    charged, free = slots[:len(scorers)], slots[len(scorers):]
    held, moved = {}, []
    for r in stayers:
        if r['resp'] in free:
            free.remove(r['resp'])
            held[r['key']] = r['resp']
        else:
            moved.append(r)
    for r, slot in zip(moved, free):
        held[r['key']] = slot
    return charged, held


def finish(g, event_id, kind, is_pa, result, batted, plans, errors, wp=False, pb=False, pickoff=False, balk=False,
           batter=None, charged=None, reaches=False, breaches=False, bfirst=False):
    rules = g.rules
    bat, fld = g.bat, g.fld
    pitcher = g.team[fld].pitcher
    catcher = g.catcher(fld)
    outs_on = sum(1 for p in plans if p['to'] == 0)
    outs_after = g.outs + outs_on
    limit = rules['outs_per_half_inning']

    def seniority(p):
        return next(i for i, u in enumerate(g.team[fld].usages) if u['pitcher'] == p)

    # C33: the batter who reaches on a fielder's choice that puts out a runner takes the slot of the oldest pitcher among the runners put out
    if result == 'fielders_choice':
        batter_plan = next((p for p in plans if p['frm'] == 0), None)
        out_runners = [p['runner'] for p in plans if p['frm'] >= 1 and p['to'] == 0]
        if batter_plan is not None and 1 <= batter_plan['to'] <= 3 and out_runners:
            batter_plan['runner'] = dict(batter_plan['runner'], resp=min((r['resp'] for r in out_runners), key=seniority))
    res = resolve(g, plans, breaches, bfirst)
    crossed = sorted([p for p in plans if p['to'] == 4], key=lambda p: -p['frm'])
    scoring = [p for p in crossed if p['runner']['key'] in res['counted']]
    voided = {p['runner']['key'] for p in crossed if p['runner']['key'] not in res['counted']}
    walk_off = bat == 'home' and g.inning >= rules['regulation_innings'] and len(scoring) > 0 and g.runs['home'] + len(scoring) > g.runs['away']
    dp = outs_on >= 2 and result not in ('single', 'double', 'triple', 'home_run')
    grounded_dp = dp and grounded_into_force_dp(g, plans, res, batted, is_pa)
    credit_players = []
    for p in plans:
        if p['to'] == 0:
            for c in p['credits']:
                credit_players.append((c, g.defender(fld, c['position'])))
    rbi_play = is_pa and result not in ('reached_on_error', 'strikeout', 'strikeout_reached_base') and not grounded_dp
    runs = []
    stayers = [p['runner'] for p in sorted((p for p in plans if 1 <= p['to'] <= 3), key=lambda p: -p['to'])]
    charged_to, slots_after = settle([p['runner'] for p in scoring], stayers, seniority)
    g.divergent = getattr(g, 'divergent', 0) + sum(1 for c, p in zip(charged_to, scoring) if c != p['runner']['resp'])
    for i, p in enumerate(scoring):
        eligible = p['cause'] not in ('wild_pitch', 'passed_ball', 'steal', 'balk', 'error_extra_base', 'error_saved_out')
        runs.append({'key': p['runner']['key'], 'player': p['runner']['player'], 'resp': charged_to[i], 'cause': p['cause'],
                     'rbi': bool(rbi_play and eligible)})
    if is_pa:
        add = g.add
        add(bat, batter, 'batpa')
        add(fld, pitcher, 'pitchbf')
        add(fld, catcher, 'batterybf', 1, pitcher)
        if result not in ('walk', 'hit_by_pitch', 'sacrifice_fly', 'sacrifice_bunt'):
            add(bat, batter, 'batab')
        if result in ('single', 'double', 'triple', 'home_run'):
            add(bat, batter, 'bath')
            add(bat, batter, {'single': 'bat1b', 'double': 'bat2b', 'triple': 'bat3b', 'home_run': 'bathr'}[result])
            add(fld, pitcher, 'pitchh')
            if result == 'home_run':
                add(fld, pitcher, 'pitchhr')
        elif result == 'walk':
            add(bat, batter, 'batbb')
            add(fld, charged, 'pitchbb')
        elif result == 'hit_by_pitch':
            add(bat, batter, 'bathbp')
            add(fld, pitcher, 'pitchhbp')
        elif result in ('strikeout', 'strikeout_reached_base'):
            add(bat, batter, 'batso')
            add(fld, pitcher, 'pitchk')
        elif result == 'sacrifice_fly':
            add(bat, batter, 'batsf')
        elif result == 'sacrifice_bunt':
            add(bat, batter, 'batsh')
        elif result == 'reached_on_error':
            add(bat, batter, 'batroe')
        elif result == 'fielders_choice':
            add(bat, batter, 'batfc')
        if grounded_dp:
            add(bat, batter, 'batgidp')
        rbi = sum(1 for r in runs if r['rbi'])
        if rbi:
            add(bat, batter, 'batrbi', rbi)
    for _ in range(outs_on):
        g.add(fld, pitcher, 'pitchouts')
        g.add(fld, catcher, 'catouts')
        g.add(fld, catcher, 'batteryouts', 1, pitcher)
    for c, player in credit_players:
        g.add(fld, player, 'fldpo' if c['kind'] == 'putout' else 'flda')
    for pos in errors:
        g.add(fld, g.defender(fld, pos), 'flde')
    if dp and is_pa:
        for player in sorted({pl for _, pl in credit_players}):
            g.add(fld, player, 'flddp')
    for r in runs:
        g.add(bat, r['player'], 'runr')
        g.add(fld, r['resp'], 'pitchr')
    for p in plans:
        if p['cause'] == 'steal':
            if p['to'] == 0:
                g.add(bat, p['runner']['player'], 'runcs')
                if any(c['position'] == 'catcher' for c in p['credits']):
                    g.add(fld, catcher, 'catcs')
            else:
                g.add(bat, p['runner']['player'], 'runsb')
                g.add(fld, catcher, 'catsba')
    if pickoff:
        for p in plans:
            if p['to'] == 0:
                g.add(bat, p['runner']['player'], 'runpicked')
    if wp:
        g.add(fld, pitcher, 'pitchwp')
    if pb:
        g.add(fld, catcher, 'fldpb')
    if balk:
        g.add(fld, pitcher, 'pitchbk')

    seq = len(g.plays) + 1
    play = {'seq': seq, 'bat': bat, 'inning': g.inning, 'half': g.half, 'batter': batter if is_pa else None, 'outs_before': g.outs, 'voided': sorted(voided),
            'moves': [{'key': p['runner']['key'], 'frm': p['frm'], 'to': p['to'], 'cause': p['cause'],
                       'oseq': res['seq'].get(p['runner']['key'], 0), 'cls': res['cls'].get(p['runner']['key'])} for p in plans],
            'runs': runs, 'batting_pitcher': g.team[bat].pitcher, 'fielding_pitcher': pitcher}
    g.plays.append(play)

    new_bases = {1: None, 2: None, 3: None}
    for p in plans:
        if 1 <= p['to'] <= 3:
            new_bases[p['to']] = dict(p['runner'], resp=slots_after[p['runner']['key']])
    g.bases = new_bases
    g.runs[bat] += len(runs)
    if runs:
        g.line[bat][-1] += len(runs)
    g.outs = outs_after
    u = g.team[fld].usage()
    u['outs'] += outs_on
    if is_pa:
        u['bf'] += 1
        g.team[bat].next += 1
        g.runner_key += 1
        g.balls = g.strikes = 0
        g.stage = 'start'
        g.walk_override = None
    if outs_after == limit or walk_off:
        close_half(g, event_id, outs_after == limit, walk_off)


def close_half(g, event_id, half_over, walk_off):
    rules = g.rules
    fld = g.fld
    half = [p for p in g.plays if p['inning'] == g.inning and p['half'] == g.half]
    # earned runs: a pitcher's run is judged in the inning as reconstructed from the moment HE entered (OBR 9.16(i)); the first pitcher of the half is judged from its start
    views = {}

    def view(pitcher):
        entry = [u for u in g.team[fld].usages if u['pitcher'] == pitcher][-1]['entry_seq']
        start = next((i for i, pl in enumerate(half) if pl['seq'] > entry), 0)
        if start not in views:
            views[start] = reconstruct(half, rules['outs_per_half_inning'], start)
        return views[start]

    for p in half:
        for r in p['runs']:
            if r['key'] in view(r['resp']):
                g.add(fld, r['resp'], 'pitcher')  # StatKey.PitchER lower-cased
    # left on base: runners standing on base at the end of the half (or of a walk-off game by profile rule) plus each runner whose run did not count (never out, never scored)
    stranded = g.runner_count() if (half_over or (walk_off and rules['count_walk_off_runners_as_left_on_base'])) else 0
    stranded += len({k for pl in half for k in pl['voided']})
    if stranded:
        g.add(g.bat, '', 'teamlob', stranded)
    if walk_off:
        g.status, g.outcome = 'final', 'home_win'
        return
    reg = rules['regulation_innings']
    if g.half == 'top':
        if g.inning >= reg and g.runs['home'] > g.runs['away']:
            g.status, g.outcome = 'final', 'home_win'
            return
        next_half(g, g.inning, 'bottom')
        return
    if g.inning >= reg:
        if g.runs['away'] != g.runs['home']:
            g.status, g.outcome = 'final', 'away_win' if g.runs['away'] > g.runs['home'] else 'home_win'
            return
        if rules['extra_innings_limit'] is not None and g.inning >= rules['extra_innings_limit']:
            g.status, g.outcome = 'final', 'tie'
            return
    next_half(g, g.inning + 1, 'top')


def next_half(g, inning, half):
    g.inning, g.half = inning, half
    g.outs = 0
    g.balls = g.strikes = 0
    g.bases = {1: None, 2: None, 3: None}
    g.stage = 'start'
    g.walk_override = None
    g.half_ended = True
    if half == 'top':
        g.line['away'].append(0)
    else:
        g.line['home'].append(0)


def reconstruct(half, limit, start=0):
    recon = {}
    earned = set()
    outs = 0
    if 0 < start < len(half):
        # a relief pitcher's view: the real outs and the real runners when he entered are the starting position
        for before in half[:start]:
            for m in before['moves']:
                if 1 <= m['to'] <= 3:
                    recon[m['key']] = m['to']
                else:
                    recon.pop(m['key'], None)
        outs = half[start]['outs_before']
    for pl in half[start:]:
        if outs >= limit:
            break
        moves = {m['key']: m for m in pl['moves']}
        bm = next((m for m in pl['moves'] if m['frm'] == 0), None)
        batter_reaches = bm is not None and (bm['to'] != 0 or bm['cls'] == 'nonforce') and bm['cause'] not in ('error_saved_out', 'passed_ball')
        nxt, terms, blocker = {}, [], 99

        def place(dest, floor, key):
            nonlocal blocker
            if dest >= 4:
                terms.append({'key': key, 'out': False, 'score': True, 'seq': moves[key]['oseq'], 'cls': None})
                return
            dest = max(floor, min(dest, blocker - 1))
            nxt[key] = dest
            blocker = min(blocker, dest)

        for key, rb in sorted(recon.items(), key=lambda kv: -kv[1]):
            m = moves.get(key)
            if m is None:
                dest = rb
            elif m['cause'] in ('passed_ball', 'error_extra_base'):
                forced = batter_reaches and all(b in recon.values() for b in range(1, rb))
                dest = rb + 1 if forced else rb
            elif m['to'] == 0 or m['cause'] == 'error_saved_out':
                terms.append({'key': key, 'out': True, 'score': False, 'seq': m['oseq'], 'cls': m['cls'] or 'nonforce'})
                continue
            else:
                dest = min(4, rb + (m['to'] - m['frm']))
            place(dest, rb, key)
        if bm is not None:
            if bm['to'] == 0 or bm['cause'] in ('error_saved_out', 'passed_ball'):
                terms.append({'key': bm['key'], 'out': True, 'score': False, 'seq': bm['oseq'], 'cls': bm['cls'] if bm['to'] == 0 and bm['cls'] == 'nonforce' else 'batter'})
            else:
                place(min(4, bm['to']), 1, bm['key'])
        ordered = sorted(terms, key=lambda t: t['seq'])
        o = outs
        third = None
        for t in ordered:
            if t['out']:
                o += 1
                if o == limit and third is None:
                    third = t
        for t in ordered:
            if t['score'] and (third is None or (third['cls'] == 'nonforce' and t['seq'] < third['seq'])):
                earned.add(t['key'])
        if third is not None:
            outs = limit
            break
        recon = nxt
        outs = o
    return earned


# ----------------------------------------------------------------------------------------------- substitutions

def apply_sub(g, ev):
    side = ev['team']
    t = g.team[side]
    kind = ev['kind']
    rules = g.rules

    def enter(player, into_lineup):
        t.bench.remove(player)
        if player not in t.appeared:
            t.appeared.append(player)
        if into_lineup and player not in t.ever:
            t.ever.append(player)

    def leave(player):
        if player not in t.removed:
            t.removed.append(player)

    if kind == 'pinch_hit':
        slot = t.batter_slot()
        leave(slot['player'])
        enter(ev['player'], True)
        slot['player'] = ev['player']
    elif kind == 'pinch_run':
        b = ev['base']
        r = g.bases[b]
        slot = t.slot(r['order'])
        leave(r['player'])
        enter(ev['player'], True)
        slot['player'] = ev['player']
        r['player'] = ev['player']
    elif kind == 'defensive_sub':
        slot = t.slot(ev['slot'])
        leave(slot['player'])
        enter(ev['player'], True)
        slot['player'] = ev['player']
    elif kind == 'position_swap':
        a, b = t.slot(ev['slot']), t.slot(ev['slot2'])
        a['pos'], b['pos'] = b['pos'], a['pos']
    else:
        old = t.pitcher
        new = ev['player']
        if new in t.bench:
            enter(new, not rules['designated_hitter'])
            if not rules['designated_hitter']:
                ps = t.slot_at('pitcher')
                leave(ps['player'])
                if ps['player'] != old:
                    leave(old)
                ps['player'] = new
            elif t.slot_of(old) is None:
                leave(old)
        else:
            slot = t.slot_of(new)
            if rules['designated_hitter']:
                if t.slot_of(old) is None:
                    leave(old)
            else:
                ps = t.slot_at('pitcher')
                slot['pos'], ps['pos'] = 'pitcher', slot['pos']
        own, theirs = g.runs[side], g.runs[OTHER[side]]
        mid_walk = g.stage == 'decision' and g.balls >= 2 and g.balls < rules['balls_for_walk'] and g.balls > g.strikes  # OBR 9.16(h)(1)
        if mid_walk:
            g.walk_override = g.walk_override or old
        t.pitcher = new
        if new not in t.appeared:
            t.appeared.append(new)
        t.usages.append(Game._usage(new, len(g.plays), own - theirs, g.runner_count()))
        t.pitched.append(new)


# ----------------------------------------------------------------------------------------------- final numbers

def decisions(g):
    if g.outcome == 'tie':
        return {}
    win = 'away' if g.outcome == 'away_win' else 'home'
    lose = OTHER[win]
    wr = lr = 0
    take = None
    for p in g.plays:
        for r in p['runs']:
            before = wr - lr
            if p['bat'] == win:
                wr += 1
            else:
                lr += 1
            after = wr - lr
            if before <= 0 and after > 0:
                take = (p['batting_pitcher'], r['resp'])
            elif after <= 0:
                take = None
    if take is None:
        return {}
    wt = g.team[win]
    wp = take[0]
    u = [x for x in wt.usages if x['pitcher'] == wp][0]
    if wp == wt.starter and u['outs'] < g.rules['starter_win_min_outs']:
        rel = [x for x in wt.usages if x['pitcher'] != wt.starter]
        if rel:
            rel.sort(key=lambda x: (-x['outs'], x['entry_seq']))
            wp = rel[0]['pitcher']
    out = {'win': wp, 'loss': take[1]}
    fin = wt.usages[-1]
    if fin['pitcher'] != wp and fin['lead'] > 0 and lead_held(g, win, fin['entry_seq']):
        r = g.rules
        if (fin['lead'] <= r['save_max_lead'] and fin['outs'] >= r['save_min_outs']) or fin['lead'] <= fin['runners'] + 2 or fin['outs'] >= r['save_long_outs']:
            out['save'] = fin['pitcher']
    return out


def lead_held(g, win, entry_seq):
    wr = lr = 0
    for p in g.plays:
        for _ in p['runs']:
            if p['bat'] == win:
                wr += 1
            else:
                lr += 1
        if p['seq'] > entry_seq and wr - lr <= 0:
            return False
    return True


def expected(g):
    players = {}
    for (team, player, key, amount, partner) in g.stats:
        if player == '' or key.startswith('battery') or key == 'teamlob':
            continue
        players.setdefault(player, {})
        players[player][key] = players[player].get(key, 0) + amount
    players = {p: {k: v for k, v in sorted(d.items()) if v != 0} for p, d in sorted(players.items())}

    def total(team, key):
        return sum(a for (t, pl, k, a, pr) in g.stats if t == team and k == key)

    catchers = {}
    for (team, player, key, amount, partner) in g.stats:
        if key in ('catpitches', 'catouts', 'catsba', 'catcs'):
            c = catchers.setdefault(player, {'pitches': 0, 'outs': 0, 'sba': 0, 'cs': 0, 'pb': 0})
            c[{'catpitches': 'pitches', 'catouts': 'outs', 'catsba': 'sba', 'catcs': 'cs'}[key]] += amount
    for c, d in catchers.items():
        d['pb'] = sum(a for (t, pl, k, a, pr) in g.stats if pl == c and k == 'fldpb')
    batteries = {}
    for (team, player, key, amount, partner) in g.stats:
        if key in ('batterypitches', 'batteryouts', 'batterybf'):
            b = batteries.setdefault(f'{player}/{partner}', {'pitches': 0, 'outs': 0, 'bf': 0})
            b[{'batterypitches': 'pitches', 'batteryouts': 'outs', 'batterybf': 'bf'}[key]] += amount
    appear = []
    for side in ('away', 'home'):
        t = g.team[side]
        for p in sorted(t.appeared):
            appear.append(f'{side}:{p}:{int(p in t.ever)}:{int(p in t.pitched)}')
    return {
        'outcome': g.outcome,
        'away_runs': g.runs['away'], 'home_runs': g.runs['home'],
        'away_line': g.line['away'], 'home_line': g.line['home'],
        'away_hits': total('away', 'bath'), 'home_hits': total('home', 'bath'),
        'away_errors': total('away', 'flde'), 'home_errors': total('home', 'flde'),
        'away_lob': total('away', 'teamlob'), 'home_lob': total('home', 'teamlob'),
        'players': players,
        'catchers': dict(sorted(catchers.items())),
        'batteries': dict(sorted(batteries.items())),
        'decisions': decisions(g),
        'appearances': appear,
    }


# ------------------------------------------------------------------------------------------------------ the generator

def seqm(move, sequence, out_at=None):
    move['sequence'] = sequence
    if out_at is not None:
        move['out_at'] = out_at
    return move


def third_out_scenario(g, env, need):
    """Two outs, runners on first and third: a play whose third out and a run come in a stated order (docs/game-engine.md 4)."""
    rng = env.rng
    occupied = {b for b in (1, 2, 3) if g.bases[b]}
    if occupied != {1, 3} or (need is not None and need < 2):
        return False
    if rng.random() > 0.55:
        return False
    tag3 = [co('right_field', 'assist'), co('third_base', 'putout')]
    force2 = [co('shortstop', 'assist'), co('second_base', 'putout')]
    kind = rng.choice(['run_then_tag', 'tag_then_run', 'force', 'batter_out_after_run', 'batter_out_first'])
    ev = {'type': 'in_play'}
    if kind == 'run_then_tag':
        ev.update({'batted': 'line', 'result': 'single', 'moves': [seqm(mv(3, 4), 1), seqm(mv(1, 0, 'play', 'tag', tag3), 2, 3)]})
    elif kind == 'tag_then_run':
        ev.update({'batted': 'line', 'result': 'single', 'moves': [seqm(mv(3, 4), 2), seqm(mv(1, 0, 'play', 'tag', tag3), 1, 3)]})
    elif kind == 'force':
        order = rng.choice([(1, 2), (2, 1)])
        ev.update({'batted': 'ground', 'result': 'fielders_choice', 'moves': [seqm(mv(3, 4), order[0]), seqm(mv(1, 0, 'play', 'force', force2), order[1])]})
    elif kind == 'batter_out_after_run':
        ev.update({'batted': 'ground', 'result': 'out', 'moves': [seqm(mv(3, 4), 1), mv(1, 2)], 'batter_sequence': 2,
                   'batter_credits': [co('shortstop', 'assist'), co('first_base', 'putout')]})
    else:
        ev.update({'batted': 'ground', 'result': 'out', 'moves': [seqm(mv(3, 4), 2), mv(1, 2)], 'batter_sequence': 1,
                   'batter_credits': [co('shortstop', 'assist'), co('first_base', 'putout')]})
    emit(g, ev)
    return True


def post_reach_scenario(g, env, need):
    """A single whose batter is put out at second: the run counts if it crossed before a third out, and never if the out came first."""
    rng = env.rng
    occupied = {b for b in (1, 2, 3) if g.bases[b]}
    if occupied not in ({3}, {1, 3}) or (need is not None and need < 2) or rng.random() > 0.5:
        return False
    third = g.outs == g.rules['outs_per_half_inning'] - 1
    run_first = rng.choice([True, False]) if third else True
    run = mv(3, 4)
    batter = mv(0, 0, 'play', 'tag', [co('right_field', 'assist'), co('second_base', 'putout')])
    batter['out_at'] = 2
    if third:
        seqm(run, 1 if run_first else 2)
        batter['sequence'] = 2 if run_first else 1
    moves = [run] + ([mv(1, 2)] if 1 in occupied else []) + [batter]
    emit(g, {'type': 'in_play', 'batted': 'line', 'result': 'single', 'moves': moves})
    return True


def mv(frm, to, cause='play', out_kind=None, credits=None):
    m = {'from': frm, 'to': to}
    if cause != 'play':
        m['cause'] = cause
    if out_kind:
        m['out_kind'] = out_kind
    if credits:
        m['credits'] = credits
    return m


def emit(g, ev):
    ev['id'] = g.next_id()
    apply_event(g, ev)


class Env:
    """Run-environment knobs so the 100 games cover low-scoring extra innings as well as slugfests."""

    def __init__(self, rng, hit, walk, sub, fc=0.16, pitch_only=False):
        self.rng, self.hit, self.walk, self.sub, self.fc, self.pitch_only = rng, hit, walk, sub, fc, pitch_only


def runners_lead_first(g):
    return [(b, g.bases[b]) for b in (3, 2, 1) if g.bases[b]]


def walk_off_need(g):
    """Runs the home team still needs to win right now, or None when this is not a decisive bottom half."""
    if g.bat != 'home' or g.inning < g.rules['regulation_innings']:
        return None
    return g.runs['away'] - g.runs['home'] + 1


def lowest_scoring(g, n):
    """How many runners MUST score on an n-base hit (they cannot stay ahead of the batter)."""
    runners = runners_lead_first(g)
    k = len(runners)
    return sum(1 for i, (b, _) in enumerate(runners) if max(b, min(4, n + (k - i))) == 4)


def hit_moves(g, n, rng, minimal):
    """Moves for runners on an n-base hit; with minimal=True nobody advances beyond what the batter forces."""
    runners = runners_lead_first(g)
    moves = []
    prev = 99
    k = len(runners)
    for i, (b, r) in enumerate(runners):
        j = k - i  # 1 = trail-most runner
        lo = max(b, min(4, n + j))
        hi = 4 if prev >= 4 else prev - 1
        target = lo if minimal else min(4, b + n + (1 if rng.random() < 0.15 else 0))
        dest = max(lo, min(target, hi))
        moves.append(mv(b, dest))
        prev = dest
    return moves


def pick(rng, items):
    return items[rng.randrange(len(items))]


def credit_for_out(rng, batted):
    if batted == 'fly':
        return [co(pick(rng, OUTFIELD), 'putout')]
    if batted in ('line', 'pop'):
        return [co(pick(rng, INFIELD + OUTFIELD), 'putout')]
    return [co(pick(rng, ['shortstop', 'third_base', 'second_base']), 'assist'), co('first_base', 'putout')]


def do_in_play(g, env):
    rng = env.rng
    r = rng.random()
    outs_before = g.outs
    runners = runners_lead_first(g)
    n = len(runners)
    limit = g.rules['outs_per_half_inning']
    end_risk = outs_before == limit - 1
    need = walk_off_need(g)
    if outs_before == limit - 1 and third_out_scenario(g, env, need):
        return
    if post_reach_scenario(g, env, need):
        return
    ev = {'type': 'in_play'}
    hit = env.hit
    if r < hit * 0.62:
        res, bases = 'single', 1
    elif r < hit * 0.82:
        res, bases = 'double', 2
    elif r < hit * 0.86:
        res, bases = 'triple', 3
    elif r < hit * 0.94:
        res, bases = 'home_run', 4
    else:
        res, bases = None, 0
    if res in ('single', 'double', 'triple'):
        minimal = False
        if need is not None:
            # a decisive bottom half: never score more runs than win the game (the reducer rejects extra runs)
            while bases > 1 and lowest_scoring(g, bases) > need:
                bases -= 1
            res = {1: 'single', 2: 'double', 3: 'triple'}[bases]
            minimal = True
        ev.update({'batted': rng.choice(['ground', 'line', 'fly']), 'result': res, 'moves': hit_moves(g, bases, rng, minimal)})
        emit(g, ev)
        return
    if res == 'home_run':
        ev.update({'batted': 'fly', 'result': 'home_run'})
        emit(g, ev)
        return
    u = rng.random()
    if n and u < 0.04 and outs_before < limit - 1:
        moves = [mv(b, b + 1) for b, _ in runners]
        ev.update({'batted': 'bunt', 'result': 'out', 'sacrifice': True, 'moves': moves,
                   'batter_credits': [co('third_base', 'assist'), co('first_base', 'putout')]})
        emit(g, ev)
        return
    if u < 0.10:
        moves = []
        for b, _ in sorted(runners, key=lambda x: x[0]):
            forced = all(g.bases[x] for x in range(1, b))
            moves.append(mv(b, b + 1 if forced else b))
        ev.update({'batted': 'ground', 'result': 'reached_on_error', 'moves': moves, 'errors': [pick(rng, INFIELD)]})
        emit(g, ev)
        return
    if g.bases[1] and outs_before < limit - 1 and u < env.fc:
        # the C33 set also retires the LEAD runner of the forced chain (runners behind him are forced up one base): the slot he held passes to the batter
        chain = [b for b in (1, 2, 3) if all(g.bases[x] for x in range(1, b + 1))]
        out_base = chain[-1] if (env.fc > 0.16 and rng.random() < 0.9) else 1
        putout_at = {1: 'second_base', 2: 'third_base', 3: 'catcher'}
        moves = []
        for b, _ in runners:
            if b == out_base:
                moves.append(mv(b, 0, 'play', 'force', [co('shortstop', 'assist'), co(putout_at[b], 'putout')]))
            elif b < out_base:
                moves.append(mv(b, b + 1))
            else:
                moves.append(mv(b, b))
        ev.update({'batted': 'ground', 'result': 'fielders_choice', 'moves': moves})
        emit(g, ev)
        return
    if g.bases[1] and outs_before == 0 and u < max(0.40, env.fc):
        moves = []
        for b, _ in runners:
            if b == 1:
                moves.append(mv(1, 0, 'play', 'force', [co('shortstop', 'assist'), co('second_base', 'putout')]))
            else:
                moves.append(mv(b, b))
        ev.update({'batted': 'ground', 'result': 'out', 'moves': moves, 'batter_credits': [co('second_base', 'assist'), co('first_base', 'putout')]})
        emit(g, ev)
        return
    batted = rng.choice(['ground', 'ground', 'fly', 'fly', 'line', 'pop'])
    moves = []
    prev = 99
    for b, _ in runners:
        dest = b
        if not end_risk:
            if batted == 'fly' and b == 3 and rng.random() < 0.6:
                dest = 4
            elif batted == 'ground' and rng.random() < 0.45:
                dest = b + 1
        hi = 4 if prev >= 4 else prev - 1
        dest = max(b, min(dest, hi))
        moves.append(mv(b, dest))
        prev = dest
    ev.update({'batted': batted, 'result': 'out', 'moves': moves, 'batter_credits': credit_for_out(rng, batted)})
    emit(g, ev)


def try_runner_play(g, env):
    """Occasionally a steal, pickoff or balk between pitches. Returns True if something was played."""
    rng = env.rng
    if g.runner_count() == 0:
        return False
    if g.outs == g.rules['outs_per_half_inning'] - 1 and {b for b in (1, 2, 3) if g.bases[b]} == {1, 3}:
        need = walk_off_need(g)
        if (need is None or need >= 2) and rng.random() < 0.3:
            cs = [co('catcher', 'assist'), co('second_base', 'putout')]
            first = rng.choice([True, False])
            emit(g, {'type': 'runner_play', 'kind': 'steal',
                     'moves': [seqm(mv(3, 4, 'steal'), 1 if first else 2), seqm(mv(1, 0, 'steal', 'tag', cs), 2 if first else 1)]})
            return True
    u = rng.random()
    if u < 0.04:
        for b in (3, 2, 1):
            if not g.bases[b]:
                continue
            if b == 3 and rng.random() < 0.7:
                continue
            if b < 3 and g.bases[b + 1]:
                continue
            if rng.random() < 0.72:
                emit(g, {'type': 'runner_play', 'kind': 'steal', 'moves': [mv(b, b + 1, 'steal')]})
            else:
                credits = [co('catcher', 'putout')] if b == 3 else [co('catcher', 'assist'), co('second_base' if b == 1 else 'third_base', 'putout')]
                emit(g, {'type': 'runner_play', 'kind': 'steal', 'moves': [mv(b, 0, 'steal', 'tag', credits)]})
            return True
        return False
    if u < 0.055:
        b = rng.choice([x for x in (1, 2, 3) if g.bases[x]])
        base_pos = {1: 'first_base', 2: 'shortstop', 3: 'third_base'}[b]
        emit(g, {'type': 'runner_play', 'kind': 'pickoff', 'moves': [mv(b, 0, 'play', 'tag', [co('pitcher', 'assist'), co(base_pos, 'putout')])]})
        return True
    if u < 0.065:
        emit(g, {'type': 'runner_play', 'kind': 'balk', 'moves': [mv(b, b + 1, 'balk') for b in (3, 2, 1) if g.bases[b]]})
        return True
    return False


def do_pitch(g, env, take):
    rng = env.rng
    u = rng.random()
    limit_k = g.rules['strikes_for_strikeout']
    count_k = g.strikes == limit_k - 1
    ball4 = g.balls == g.rules['balls_for_walk'] - 1
    runners = g.runner_count() > 0
    ev = {'type': 'take' if take else 'pitch'}
    if take:
        ev['outcome'] = 'hit_by_pitch' if u < 0.012 else 'ball' if u < 0.5 else 'called_strike'
        emit(g, ev)
        return
    w = env.walk
    t1, t2, t3, t4 = 0.012 + w, 0.012 + w + 0.14, 0.012 + w + 0.27, 0.012 + w + 0.44
    if u < 0.012:
        ev['outcome'] = 'hit_by_pitch'
    elif u < t1:
        ev['outcome'] = 'ball'
    elif u < t2:
        ev['outcome'] = 'called_strike'
    elif u < t3:
        ev['outcome'] = 'swinging_strike'
    elif u < t4:
        ev['outcome'] = 'foul' if rng.random() > 0.05 else 'foul_bunt'
    else:
        do_in_play(g, env)
        return
    outcome = ev['outcome']
    k_ending = outcome in ('called_strike', 'swinging_strike', 'foul_bunt') and count_k
    non_ending = not (k_ending or (outcome == 'ball' and ball4) or outcome == 'hit_by_pitch')
    if non_ending and runners and outcome in ('ball', 'called_strike', 'swinging_strike') and rng.random() < 0.05:
        mishap = 'wild_pitch' if rng.random() < 0.55 else 'passed_ball'
        ev['mishap'] = mishap
        ev['moves'] = [mv(b, b + 1, mishap) for b in (3, 2, 1) if g.bases[b]]
    elif k_ending and outcome != 'foul_bunt' and (g.bases[1] is None or g.outs == g.rules['outs_per_half_inning'] - 1) and rng.random() < 0.08:
        ev['dropped_third'] = 'wild_pitch' if rng.random() < 0.5 else 'passed_ball'
    elif k_ending and rng.random() < 0.03:
        ev['strikeout_credits'] = [co('catcher', 'assist'), co('first_base', 'putout')]
    emit(g, ev)


def dh_must_bat(g, side, player):
    """OBR 5.11(a)(2): the starting DH may not be replaced before his first plate appearance unless the opposing club has changed pitchers."""
    t = g.team[side]
    if not g.rules['designated_hitter'] or t.starting_dh != player:
        return False
    if len(g.team[OTHER[side]].usages) > 1:
        return False
    if any(p['bat'] == side and p['batter'] == player for p in g.plays):
        return False
    return not (side == g.bat and g.stage != 'start' and t.batter_slot()['player'] == player)


def maybe_substitute(g, env):
    rng = env.rng
    if g.status != 'play' or g.stage == 'insequence':
        return
    fld, bat = g.fld, g.bat
    if not g.defense_complete(fld):
        emit(g, {'type': 'substitution', 'kind': 'pitching_change', 'team': fld, 'player': g.team[fld].bench[0]})
        return
    if rng.random() > env.sub:
        return
    dh_rule = g.rules['designated_hitter']
    tb, tf = g.team[bat], g.team[fld]
    if env.pitch_only:
        if len(tf.bench) >= 2 and g.runner_count() > 0:   # the reliever inherits runners, which is what C33 is about
            emit(g, {'type': 'substitution', 'kind': 'pitching_change', 'team': fld, 'player': tf.bench[0]})
        return
    u = rng.random()

    def two_way(t, player):
        return dh_rule and player == t.pitcher and t.slot_of(player) is not None and t.slot_of(player)['pos'] == 'designated_hitter'

    def reserve_ok(t, slot_pos):
        # replacing the batting pitcher (no DH) needs one more bench player for the forced pitching change
        return dh_rule or slot_pos != 'pitcher' or len(t.bench) >= 2

    if u < 0.25 and tb.bench:
        cur = tb.batter_slot()
        if two_way(tb, cur['player']) or not reserve_ok(tb, cur['pos']) or dh_must_bat(g, bat, cur['player']):
            return
        emit(g, {'type': 'substitution', 'kind': 'pinch_hit', 'team': bat, 'player': tb.bench[0]})
    elif u < 0.5 and tb.bench and g.runner_count() > 0:
        bases = [b for b in (1, 2, 3) if g.bases[b] and not two_way(tb, g.bases[b]['player']) and reserve_ok(tb, tb.slot(g.bases[b]['order'])['pos'])]
        if bases:
            emit(g, {'type': 'substitution', 'kind': 'pinch_run', 'team': bat, 'player': tb.bench[0], 'base': rng.choice(bases)})
    elif u < 0.65:
        side = rng.choice(['away', 'home'])
        t = g.team[side]
        if not t.bench:
            return
        occupied = {r['order'] for r in (g.bases[b] for b in (1, 2, 3)) if r} if side == bat else set()
        cur = t.batter_slot()['order'] if side == bat else None
        cands = [s['order'] for s in t.lineup if s['pos'] != 'pitcher' and s['order'] not in occupied and s['order'] != cur and not two_way(t, s['player'])
                 and not (s['pos'] == 'designated_hitter' and dh_must_bat(g, side, s['player']))]
        if cands:
            emit(g, {'type': 'substitution', 'kind': 'defensive_sub', 'team': side, 'player': t.bench[0], 'slot': rng.choice(cands)})
    elif u < 0.75:
        side = rng.choice(['away', 'home'])
        cands = [s['order'] for s in g.team[side].lineup if s['pos'] in DEFENSE]
        a, b = rng.sample(cands, 2)
        emit(g, {'type': 'substitution', 'kind': 'position_swap', 'team': side, 'slot': a, 'slot2': b})
    elif tf.bench and (tf.usage()['bf'] >= 14 or rng.random() < 0.4):
        emit(g, {'type': 'substitution', 'kind': 'pitching_change', 'team': fld, 'player': tf.bench[0]})


def play_pa(g, env):
    rng = env.rng
    g.half_ended = False
    inning_before, half_before = g.inning, g.half
    steps = 0
    while g.status == 'play':
        steps += 1
        if steps > 40:
            raise RuntimeError('plate appearance did not end')
        if g.stage in ('start', 'decision'):
            maybe_substitute(g, env)
            if g.status != 'play':
                return
            if not g.defense_complete(g.fld):
                continue
            if rng.random() < 0.11:
                do_pitch(g, env, take=True)
                if g.stage == 'start':
                    return
                continue
            emit(g, {'type': 'approach', 'approach': rng.choice(['normal', 'patient', 'aggressive'])})
            continue
        if try_runner_play(g, env):
            if g.status != 'play' or (g.inning, g.half) != (inning_before, half_before):
                return
            continue
        do_pitch(g, env, take=False)
        if g.stage == 'start':
            return


def make_game(index, rules, seed, two_way_side=None, stress=False):
    rng = random.Random(seed)
    away = Team('a', rules, two_way_side == 'away')
    home = Team('h', rules, two_way_side == 'home')
    g = Game(f'g{index:03d}', rules, away, home)
    env = Env(rng, hit=rng.choice([0.12, 0.2, 0.3, 0.4]), walk=rng.choice([0.2, 0.28, 0.34]), sub=rng.choice([0.03, 0.08, 0.15]))
    if stress:
        # C33 set: many pitching changes and many fielder's choices, so runs are charged to slots that moved (OBR 9.16(g))
        env = Env(rng, hit=rng.choice([0.2, 0.3]), walk=0.5, sub=0.3, fc=0.7, pitch_only=True)
    guard = 0
    while g.status == 'play':
        guard += 1
        if guard > 900:
            raise RuntimeError('game too long')
        play_pa(g, env)
    return g, away, home


def event_json(ev):
    out = {k: v for k, v in ev.items()}
    if out.get('moves') == []:
        del out['moves']
    return out


def build(profile_file, out_100, out_diag, out_c33=None):
    profiles = {p['profile_id']: p for p in json.load(open(profile_file, encoding='utf-8'))['profiles']}
    plan = ([('test-dh-cap11', 40), ('test-no-dh-cap11', 15), ('test-dh-unlimited', 15), ('test-dh-cap10', 10),
             ('test-dh-no-two-way', 10), ('kbo-oriented-unverified-v0', 10)])
    games = []
    index = 0
    for pid, count in plan:
        rules = profiles[pid]
        for i in range(count):
            index += 1
            seed = 1000 + index * 7919
            two_way = None
            if rules['two_way_player_rule'] and i % 4 == 1:
                two_way = 'away' if i % 8 == 1 else 'home'
            g = None
            for attempt in range(60):
                try:
                    g, away, home = make_game(index, rules, seed + attempt, two_way)
                    break
                except RuntimeError:
                    continue
            if g is None:
                raise SystemExit(f'could not generate game {index}')
            # original rosters
            a0 = Team('a', rules, two_way == 'away')
            h0 = Team('h', rules, two_way == 'home')
            games.append({
                'id': g.id, 'title': f'{pid} 스크립트 경기 {index}', 'rules': pid,
                'away': {'lineup': [{'order': s_['order'], 'player': s_['player'], 'position': s_['pos']} for s_ in a0.lineup], 'pitcher': a0.starter, 'bench': list(a0.bench)},
                'home': {'lineup': [{'order': s_['order'], 'player': s_['player'], 'position': s_['pos']} for s_ in h0.lineup], 'pitcher': h0.starter, 'bench': list(h0.bench)},
                'events': [event_json(e) for e in g.events],
                'expected': expected(g),
            })
    with open(out_100, 'w', encoding='utf-8', newline='\n') as f:
        json.dump({'format_version': 1, 'label': '가상 스크립트 경기 100개 (독립 Python 스코어러가 만든 기대 결과 포함)', 'games': games}, f, ensure_ascii=False, separators=(',', ':'))
        f.write('\n')
    # a small embedded set for the diagnostic screen: the first few games plus the first of each notable ending
    picks = []
    seen = set()
    for gm in games:
        key = (gm['expected']['outcome'], gm['rules'] == 'test-no-dh-cap11')
        if key not in seen and len(picks) < 6:
            seen.add(key)
            picks.append(gm)
    with open(out_diag, 'w', encoding='utf-8', newline='\n') as f:
        json.dump({'format_version': 1, 'label': '가상 진단 경기 (Phase 2)', 'games': picks}, f, ensure_ascii=False, separators=(',', ':'))
        f.write('\n')
    stats = {}
    for gm in games:
        stats[gm['expected']['outcome']] = stats.get(gm['expected']['outcome'], 0) + 1
    if out_c33:
        c33 = []
        meta = {}
        attempt = 0
        # stress games are drawn until 30 of them contain a run that was charged through a moved slot (a candidate is kept only if it does)
        for pid, count in [('test-dh-cap11', 20), ('test-no-dh-cap11', 10)]:
            rules = profiles[pid]
            kept = 0
            while kept < count:
                attempt += 1
                if attempt > 6000:
                    raise SystemExit('C33 stress games: not enough divergent candidates')
                try:
                    g, away, home = make_game(len(c33) + 1, rules, 50000 + attempt * 104729, None, stress=True)
                except (RuntimeError, IndexError):
                    continue
                if getattr(g, 'divergent', 0) == 0:
                    continue
                kept += 1
                n = len(c33) + 1
                meta[f'c{n:02d}'] = g.divergent
                a0, h0 = Team('a', rules, False), Team('h', rules, False)
                c33.append({
                    'id': f'c{n:02d}', 'title': f'{pid} C33 책임 이동 경기 {n}', 'rules': pid,
                    'away': {'lineup': [{'order': s_['order'], 'player': s_['player'], 'position': s_['pos']} for s_ in a0.lineup], 'pitcher': a0.starter, 'bench': list(a0.bench)},
                    'home': {'lineup': [{'order': s_['order'], 'player': s_['player'], 'position': s_['pos']} for s_ in h0.lineup], 'pitcher': h0.starter, 'bench': list(h0.bench)},
                    'events': [event_json(e) for e in g.events],
                    'expected': expected(g),
                })
        with open(out_c33, 'w', encoding='utf-8', newline='\n') as f:
            json.dump({'format_version': 1, 'label': 'C33 책임 이동 스크립트 경기 30개 (독립 Python 스코어러 기대 결과 포함)', 'games': c33}, f, ensure_ascii=False, separators=(',', ':'))
            f.write('\n')
        # the loader rejects unknown fields, so what the set is FOR (runs charged through a moved slot, per game) lives in a sidecar file
        with open(out_c33[:-5] + '.meta.json', 'w', encoding='utf-8', newline='\n') as f:
            json.dump({'divergent_runs_per_game': meta}, f, ensure_ascii=False, indent=1)
            f.write('\n')
        print('c33 games', len(c33), 'divergent runs', sum(meta.values()), 'games with a moved slot', sum(1 for v in meta.values() if v))
    print('games', len(games), 'outcomes', stats, 'events', sum(len(gm['events']) for gm in games))


if __name__ == '__main__':
    build(sys.argv[3], sys.argv[1], sys.argv[2], sys.argv[4] if len(sys.argv) > 4 else None)
