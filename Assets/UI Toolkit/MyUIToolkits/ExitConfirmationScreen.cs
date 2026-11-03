using Photon.Pun;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public sealed class ExitConfirmationScreen : MonoBehaviour
{
    [SerializeField] private ToysGameUI gameUI;
    private Button cancel;
    private Button confirm;

    private void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        bool match = gameUI.matchInterface;
        root.Q<Label>("ExitTitle").text = match ? "Leave match?" : "Exit game?";
        root.Q<Label>("ExitDescription").text = match
            ? "Return to the lobby? Your team will continue without you."
            : "Close Toys Death Match and return to your desktop?";
        root.Q<Label>("ExitRoom").text = PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom.Name : "Toys Death Match";
        root.Q<Label>("LiveMatchNotice").EnableInClassList("exit-hidden", !match);
        cancel = root.Q<Button>("CancelExitButton");
        confirm = root.Q<Button>("ConfirmExitButton");
        confirm.text = match ? "Leave Match" : "Exit Game";
        cancel.clicked += gameUI.CancelExit;
        confirm.clicked += gameUI.ConfirmExit;
        root.schedule.Execute(() => cancel.Focus());
    }

    private void OnDisable()
    {
        if (cancel != null && gameUI) cancel.clicked -= gameUI.CancelExit;
        if (confirm != null && gameUI) confirm.clicked -= gameUI.ConfirmExit;
    }
}
