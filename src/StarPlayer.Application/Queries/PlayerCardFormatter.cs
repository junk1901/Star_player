using System.Text;

namespace StarPlayer.Application.Queries;

/// <summary>
/// Plain Korean text for the Phase 1 diagnostic player card, built only from <see cref="PlayerPublicView"/>. Keeping it here
/// (not in the Godot presenter) lets tests and the automated UI check scan exactly the text a viewer reads.
/// </summary>
public static class PlayerCardFormatter
{
    public const string HiddenNotice = "※ 실제 잠재력과 성장 유형은 어떤 화면에서도 표시되지 않습니다.";

    public static string Format(PlayerPublicView view)
    {
        var text = new StringBuilder();
        text.AppendLine($"[{view.ViewKind}]");
        text.AppendLine($"{view.DisplayName}  (ID {view.PlayerId})");
        text.AppendLine(Club(view));
        text.AppendLine($"나이 {view.Age}세 ({view.BirthDate}) · {view.Kind} · {view.Handedness} · {view.HeightCm}cm / {view.WeightKg}kg");
        if (view.Personality.Count > 0)
            text.AppendLine($"성격: {string.Join(", ", view.Personality)}");
        text.AppendLine($"출신: {view.Origin} · 입단 경로: {view.EntryPath} · 등록 신분: {view.Registration} · 소속: {view.League} · 상태: {view.CareerStatus}");
        if (view.Biography.Length > 0)
            text.AppendLine(view.Biography);

        Section(text, "기본 능력 (현재 상태와 별개)", view.BaseAbilities);
        if (view.Pitches.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("■ 구종별 능력");
            foreach (var pitch in view.Pitches)
                text.AppendLine($"  {pitch.Label} : {pitch.Display}");
        }

        Section(text, "현재 상태 (변동값, 기본 능력이 아님)", view.CurrentState);
        text.AppendLine();
        text.AppendLine($"■ 포지션과 숙련도 (선호 포지션: {view.PreferredPosition}, 포수 능력과 C 숙련도는 별개)");
        foreach (var line in view.Proficiencies)
            text.AppendLine($"  {line.Label} : {line.Display}");
        text.AppendLine();
        text.AppendLine(HiddenNotice);
        // Godot's Label counts CR and LF separately, so hand it plain LF line endings.
        return text.ToString().ReplaceLineEndings("\n");
    }

    private static string Club(PlayerPublicView view) =>
        view.ClubName is null
            ? "소속 구단 없음"
            : $"{view.ClubName} · 현재 등번호 {view.CurrentJerseyNumber ?? "없음"}" + (view.JerseySince is null ? "" : $" ({view.JerseySince}부터)");

    private static void Section(StringBuilder text, string title, IReadOnlyList<AbilityLine> lines)
    {
        text.AppendLine();
        text.AppendLine($"■ {title}");
        foreach (var group in lines.GroupBy(l => l.Group))
        {
            text.AppendLine($" [{group.Key}]");
            foreach (var line in group)
                text.AppendLine($"  {line.Label} : {line.Display}");
        }
    }
}
