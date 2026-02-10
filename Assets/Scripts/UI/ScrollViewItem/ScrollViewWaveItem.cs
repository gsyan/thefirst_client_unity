using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum EWaveState
{
    Pending,
    InProgress,
    Cleared
}

public class ScrollViewWaveItem : MonoBehaviour
{
    [SerializeField] private TMP_Text m_waveIndexText;
    [SerializeField] private Image m_backgroundImage;

    [Header("상태별 색상")]
    [SerializeField] private Color m_colorPending = new Color(0.3f, 0.3f, 0.3f, 1f);
    [SerializeField] private Color m_colorInProgress = new Color(1f, 0.8f, 0.2f, 1f);
    [SerializeField] private Color m_colorCleared = new Color(0.2f, 0.8f, 0.4f, 1f);

    private int m_waveIndex;
    private EWaveState m_state;

    public void InitializeScrollViewWaveItem(int waveIndex)
    {
        m_waveIndex = waveIndex;
        if (m_waveIndexText != null)
            m_waveIndexText.text = $"Wave {waveIndex + 1}";
        SetState(EWaveState.Pending);
    }

    public void SetState(EWaveState state)
    {
        m_state = state;
        if (m_backgroundImage == null) return;

        switch (state)
        {
            case EWaveState.Pending:
                m_backgroundImage.color = m_colorPending;
                break;
            case EWaveState.InProgress:
                m_backgroundImage.color = m_colorInProgress;
                break;
            case EWaveState.Cleared:
                m_backgroundImage.color = m_colorCleared;
                break;
        }
    }
}
