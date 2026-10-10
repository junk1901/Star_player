using Godot;

namespace StarPlayer.Client.Components;

/// <summary>Draws first/second/third base as a diamond; filled when a runner is on it. Pure display of committed state.</summary>
public partial class BaseDiamond : Control
{
    private bool _first;
    private bool _second;
    private bool _third;

    public void SetBases(bool first, bool second, bool third)
    {
        (_first, _second, _third) = (first, second, third);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var size = Size;
        var center = size / 2;
        var reach = Mathf.Min(size.X, size.Y) * 0.32f;
        DrawBase(center + new Vector2(reach, 0), _first);
        DrawBase(center + new Vector2(0, -reach), _second);
        DrawBase(center + new Vector2(-reach, 0), _third);
        DrawBase(center + new Vector2(0, reach), false, home: true);
    }

    private void DrawBase(Vector2 at, bool occupied, bool home = false)
    {
        var half = Mathf.Min(Size.X, Size.Y) * 0.1f;
        Vector2[] points = [at + new Vector2(0, -half), at + new Vector2(half, 0), at + new Vector2(0, half), at + new Vector2(-half, 0)];
        DrawColoredPolygon(points, occupied ? new Color(0.95f, 0.78f, 0.2f) : home ? new Color(0.5f, 0.5f, 0.55f) : new Color(0.22f, 0.25f, 0.3f));
        DrawPolyline([.. points, points[0]], new Color(0.8f, 0.82f, 0.88f), 3f);
    }
}
