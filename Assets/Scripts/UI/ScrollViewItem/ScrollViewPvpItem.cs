// PvP 상대방 목록 단일 아이템 - 이름/점수/공격 버튼 표시
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScrollViewPvpItem : MonoBehaviour
{
    [SerializeField] private Button m_attackButton;
    [SerializeField] private TMP_Text m_nameText;
    [SerializeField] private TMP_Text m_infoText;
    
    private PvpOpponentInfo m_opponentInfo;
    public PvpOpponentInfo OpponentInfo => m_opponentInfo;

    public void InitializeScrollViewPvpItem(PvpOpponentInfo opponentInfo, UnityEngine.Events.UnityAction onAttack)
    {
        m_opponentInfo = opponentInfo;

        m_attackButton.onClick.RemoveAllListeners();
        m_attackButton.onClick.AddListener(onAttack);

        if (m_nameText != null) m_nameText.text = Character.GetDisplayName(opponentInfo.characterName, opponentInfo.characterId);
        if (m_infoText != null) m_infoText.text = $"{opponentInfo.pvpScore}(Rank: {opponentInfo.rank})";
    }
}
