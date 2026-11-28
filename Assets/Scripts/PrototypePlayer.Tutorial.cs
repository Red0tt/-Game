using UnityEngine;

public sealed partial class PrototypePlayer
{
    [SerializeField] private bool teachFanhuaDirections;
    private int practicedFanhuaDirections;
    private const int AllFanhuaDirections = 0b1111;

    private bool AllFanhuaDirectionsPracticed => practicedFanhuaDirections == AllFanhuaDirections;

    private void RecordFanhuaDirection(Vector2Int direction)
    {
        int index = direction == Vector2Int.up ? 0 : direction == Vector2Int.down ? 1
            : direction == Vector2Int.left ? 2 : 3;
        practicedFanhuaDirections |= 1 << index;
    }

    private string FanhuaDirectionStatus(int index, string key)
    {
        return key + ((practicedFanhuaDirections & (1 << index)) != 0 ? " 已完成" : " 待尝试");
    }

    private float levelEnteredAt;
    private bool tutorialHasMoved;
    private Font tutorialFont;
    private GUIStyle tutorialText;

    internal static Font CreateChineseHudFont(int size)
    {
        return Font.CreateDynamicFontFromOSFont(
            new[] { "Songti SC", "PingFang SC", "Microsoft YaHei", "Noto Sans CJK SC" }, size);
    }

    private string GetTutorialMessage()
    {
        Vector3 position = body.position;
        Vector2Int cell = Vector2Int.RoundToInt(new Vector2(position.x, position.z));
        bool grid = Mode == PlayerMode.Grid;

        if (teachFanhuaDirections)
        {
            if (AllFanhuaDirectionsPracticed)
                return "四个方向都试过了。按 R 重新演示";
            string instruction = fanhua.Axis == 0
                ? "贴着翻花的一边，背对它走，拉开翻花"
                : "翻花保持连接且不可收拢；按 Z 撤回后尝试其他方向";
            return instruction + "\n" +
                FanhuaDirectionStatus(0, "W 上") + "    " + FanhuaDirectionStatus(1, "S 下") + "    " +
                FanhuaDirectionStatus(2, "A 左") + "    " + FanhuaDirectionStatus(3, "D 右");
        }

        switch (gameObject.scene.name)
        {
            case "Level01":
                tutorialHasMoved |= Mathf.Abs(position.x - spawnPosition.x) > 0.5f ||
                    Mathf.Abs(position.z - spawnPosition.z) > 0.5f;
                if (grid && !tutorialHasMoved)
                    return "W  A  S  D  移动";
                if (grid && cell.x == -1)
                    return "这个形态下，你没办法跳跃\n按右 Shift 切换形态";
                if (!grid && position.x >= 0.75f)
                    return "按右 Shift 切换形态";
                break;

            // Level04 is the third playable stage in the demo.
            case "Level04":
                if (Time.unscaledTime - levelEnteredAt < 7f)
                    return "按 Z 撤回操作\n按 R 重置关卡";
                break;

            case "Fanhua01":
                if (grid && fanhua != null && fanhua.Axis == 0 && fanhua.DistanceTo(cell) == 1)
                    return "背对着翻花走，拉开翻花";
                break;

            case "Fanhua02":
                if (grid && fanhua != null && fanhua.IsInitial && fanhua.DistanceTo(cell) == 1)
                    return "在翻花旁边按下 Q，以翻转翻花";
                break;

            // Fanhua07 is the fourth Fanhua stage in the demo.
            case "Fanhua07":
                if (grid && System.Array.Exists(flowers, flower =>
                        flower.Axis != 0 && flower.DistanceTo(cell) > 1))
                    return "翻花不可收拢，先观察已有形状与回程";
                break;
        }
        return null;
    }

    private void DrawTutorial(float width, float height)
    {
        if (Event.current.type != EventType.Repaint)
            return;

        string message = GetTutorialMessage();
        if (string.IsNullOrEmpty(message))
            return;

        if (tutorialText == null)
        {
            tutorialFont = CreateChineseHudFont(28);
            tutorialText = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                padding = new RectOffset(12, 12, 6, 6),
                font = tutorialFont
            };
            tutorialText.normal.textColor = HudInk;
        }

        float unit = Mathf.Min(width / 1280f, height / 720f);
        tutorialText.fontSize = Mathf.Max(1, Mathf.RoundToInt(28f * unit));
        float cardWidth = Mathf.Min(820f * unit, tutorialText.CalcSize(new GUIContent(message)).x + 38f * unit);
        float cardHeight = tutorialText.CalcHeight(new GUIContent(message), cardWidth) + 16f * unit;
        float x = (width - cardWidth) * 0.5f;
        float y = height * 0.085f;
        if (x < 222f && y < 180f)
            y = 180f;
        var card = new Rect(x, y, cardWidth, cardHeight);
        DrawFill(card, new Color(HudSurface.r, HudSurface.g, HudSurface.b, 0.56f));
        DrawFill(new Rect(card.x, card.y, card.width, Mathf.Max(1f, unit)),
            new Color(HudRim.r, HudRim.g, HudRim.b, 0.63f));
        GUI.Label(card, message, tutorialText);
    }

    private static Rect InsetHudRect(Rect rect, float amount)
    {
        return new Rect(rect.x + amount, rect.y + amount, rect.width - 2f * amount, rect.height - 2f * amount);
    }
}
