using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class PrototypeStartMenu : MonoBehaviour
{
    private const string FirstLevel = "Assets/Level/Level01";
    private Font menuFont;
    private GUIStyle title;
    private GUIStyle startButton;
    private bool starting;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            StartGame();
    }

    private void OnGUI()
    {
        if (title == null)
        {
            menuFont = PrototypePlayer.CreateChineseHudFont(48);
            title = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                font = menuFont,
                fontSize = 48
            };
            title.normal.textColor = PrototypePlayer.HudAccent;
            startButton = PrototypePlayer.CreateHudButtonStyle(title, 24);
        }

        PrototypePlayer.DrawFill(new Rect(0, 0, Screen.width, Screen.height), PrototypePlayer.HudMint);
        float scale = Mathf.Min(Screen.width / 680f, Screen.height / 440f);
        Matrix4x4 previousMatrix = GUI.matrix;
        GUI.matrix = previousMatrix * Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        try
        {
            var card = new Rect(Screen.width / scale * 0.5f - 204f,
                Screen.height / scale * 0.5f - 110f, 408f, 220f);
            PrototypePlayer.DrawPanel(card);
            GUI.Label(new Rect(card.x, card.y + 25f, card.width, 82f), "翻花", title);
            if (PrototypePlayer.DrawHudButton(
                    new Rect(card.center.x - 111f, card.y + 125f, 222f, 54f),
                    "开始游戏", startButton, PrototypePlayer.HudMint))
                StartGame();
        }
        finally
        {
            GUI.matrix = previousMatrix;
        }
    }

    private void StartGame()
    {
        if (starting)
            return;

        starting = true;
        SceneManager.LoadScene(FirstLevel);
    }

    private void OnDestroy()
    {
        if (menuFont != null)
            Destroy(menuFont);
    }
}
