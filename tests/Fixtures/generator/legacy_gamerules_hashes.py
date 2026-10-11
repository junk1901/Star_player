"""Independent oracle for the legacy (gamerules-v1) GameRuleSnapshot.ContentHash values pinned in
tests/StarPlayer.Infrastructure.Tests/GameRulesV2ProfileTests.cs (Phase 3-B1).

It rebuilds the frozen canonical text of GameRules.cs from content/rules/game_rules_profiles.json and prints SHA-256 per profile.
The values were produced on 2026-10-11 BEFORE any Phase 3-B1 code existed, so the C# test compares new code against an older, separately written computation.

Usage: python -I tests/Fixtures/generator/legacy_gamerules_hashes.py content/rules/game_rules_profiles.json
"""
import json,hashlib,sys
d=json.load(open(sys.argv[1],encoding='utf-8'))
st={'kbo_oriented_unverified':'KboOrientedUnverified','fictional_test':'FictionalTest'}
b=lambda x:'True' if x else 'False'
for p in d['profiles']:
    lim=p['extra_innings_limit']
    c='|'.join([p['profile_id'],p['version'],st[p['status']],str(p['outs_per_half_inning']),str(p['balls_for_walk']),str(p['strikes_for_strikeout']),str(p['regulation_innings']),'none' if lim is None else str(lim),b(p['tie_allowed']),b(p['designated_hitter']),b(p['two_way_player_rule']),str(p['starter_win_min_outs']),str(p['save_max_lead']),str(p['save_min_outs']),str(p['save_long_outs']),b(p['dh_may_be_forfeited']),b(p['count_walk_off_runners_as_left_on_base']),b(p['fielder_to_mound_forfeits_dh']),p['rule_policy']])
    print(p['profile_id'],hashlib.sha256(c.encode('utf-8')).hexdigest())
