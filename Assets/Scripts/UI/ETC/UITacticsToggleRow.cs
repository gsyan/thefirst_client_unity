// 전술 토글 버튼 6종(0=빔, 1=미사일, 2=함재기, 3=수리, 4=실드, 5=요격체) 한 줄 — 전투 화면(UIBattleView)과 함대관리(UIPanelFleet)가 공용으로 사용
// 상태의 단일 소스는 SpaceFleet.m_fleetInfo.tacticOptions — 클릭은 SpaceFleet.TryToggleTactic, 표시는 TacticOptionsChanged 이벤트로 갱신
// 자식 Button은 순서대로 idx에 대응하고, 각 버튼 자식의 "UsingImage" 활성화로 on/off를 표현
using UnityEngine;
using UnityEngine.UI;

public class UITacticsToggleRow : MonoBehaviour
{
    private Button[] m_buttons;
    private GameObject[] m_usingImages;
    private bool m_initialized;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void OnDestroy()
    {
        if (m_initialized == true)
            EventManager.Unsubscribe_TacticOptionsChanged(OnTacticOptionsChanged);
    }

    // 비활성 계층에서 생성된 직후 Refresh가 먼저 불릴 수 있어 지연 초기화를 보장
    private void EnsureInitialized()
    {
        if (m_initialized == true) return;
        m_initialized = true;

        EventManager.Subscribe_TacticOptionsChanged(OnTacticOptionsChanged);

        m_buttons = GetComponentsInChildren<Button>(true);
        m_usingImages = new GameObject[m_buttons.Length];
        for (int i = 0; i < m_buttons.Length; i++)
        {
            int idx = i;
            m_buttons[idx].onClick.AddListener(() => OnButtonClicked(idx));

            Transform usingImage = m_buttons[idx].transform.Find("UsingImage");
            m_usingImages[idx] = usingImage != null ? usingImage.gameObject : null;
        }
    }

    private void OnButtonClicked(int idx)
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);

        SpaceFleet myFleet = ObjectManager.Instance.GetMyFleet();
        if (myFleet == null) return;

        myFleet.TryToggleTactic(idx);
    }

    private void OnTacticOptionsChanged(int options)
    {
        for (int i = 0; i < m_usingImages.Length; i++)
        {
            if (m_usingImages[i] == null) continue;
            m_usingImages[i].SetActive((options & (1 << i)) != 0);
        }
    }

    // 패널이 뜰 때마다 호출 — 이벤트는 옵션이 실제로 바뀔 때만 발행되므로 현재 함대 상태로 직접 동기화
    // 해당 모듈이 없어도 토글 버튼은 항상 눌리게 둠(나중에 모듈을 갖추기 전에도 켜/꺼 둘 수 있어야 함) — 모듈이 없으면 실제 효과만 안 날 뿐
    public void Refresh()
    {
        EnsureInitialized();

        SpaceFleet myFleet = ObjectManager.Instance.GetMyFleet();
        if (myFleet == null) return;

        OnTacticOptionsChanged(myFleet.m_fleetInfo.tacticOptions);
    }
}
