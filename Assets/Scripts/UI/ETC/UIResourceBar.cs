// 상단 리소스 패널 - mineral / modulePoint / pvpPoint 실시간 표시
using System;
using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIResourceBar : MonoBehaviour
{
    private RectTransform m_rectTransform;
    [SerializeField] private Button  m_resetMineralInvestedButton;
    [SerializeField] private TMP_Text  m_textMineralCurrent;
    [SerializeField] private Image     m_imageMineralInvested;
    [SerializeField] private TMP_Text  m_textMineralInvested;
    [SerializeField] private TMP_Text  m_textModulePointCurrent;
    [SerializeField] private TMP_Text m_textModulePointMaxGot;
    [SerializeField] private TMP_Text m_textPvpPointCurrent;
    [SerializeField] private TMP_Text m_textPvpPointMaxGot;
    [SerializeField] private Image    m_imagePvpPointDday;
    [SerializeField] private TMP_Text m_textPvpPointDday;

    private static Color s_colorInvestedInsufficient => CommonUtility.PaletteColor("Text.Warning");

    private Color m_pvpDdayColorBase;
    private Color m_pvpDdayColorBright;

    private DateTime  m_pvpExpiry;
    private Coroutine m_ddayCoroutine;

    // 자원별 마지막 표시값 (-1 = 미초기화, 애니메이션 없이 즉시 표시)
    private long m_displayedMineral          = -1;
    private int  m_displayedInvestedMineral  = 0;
    private long m_displayedModulePoint = -1;
    private long m_displayedPvpPoint    = -1;

    // 자원별 롤링 애니메이션 코루틴 핸들
    private Coroutine m_coroutineMineral;
    private Coroutine m_coroutineModulePoint;
    private Coroutine m_coroutinePvpPoint;

    private static readonly WaitForSeconds s_wait1Sec    = new(0.5f);
    private static readonly WaitForSeconds s_waitFlicker = new(0.05f);

    void Awake()
    {
        m_rectTransform = GetComponent<RectTransform>();
    }

    void Start()
    {
        if (m_textPvpPointDday != null)
        {
            m_pvpDdayColorBase = m_textPvpPointDday.color;
            Color.RGBToHSV(m_pvpDdayColorBase, out float h, out float s, out float v);
            m_pvpDdayColorBright = Color.HSVToRGB(h, s, Mathf.Min(v * 2f, 1f));
        }

        if (m_resetMineralInvestedButton != null)
            m_resetMineralInvestedButton.onClick.AddListener(OnResetMineralInvestedButtonClicked);

        EventManager.Subscribe_CameraViewportChanged(OnCameraViewportChanged);
        RepositionForViewport();

        var commander = DataManager.Instance.m_currentCommander;
        if (commander == null) return;

        InitAll(commander);

        EventManager.Subscribe_MineralChanged(OnMineralChanged);
        EventManager.Subscribe_ModulePointChanged(OnModulePointChanged);
        EventManager.Subscribe_PvpPointChanged(OnPvpPointChanged);
        EventManager.Subscribe_InvestedMineralChanged(OnInvestedMineralChanged);
    }

    private void OnDestroy()
    {
        EventManager.Unsubscribe_CameraViewportChanged(OnCameraViewportChanged);
        EventManager.Unsubscribe_MineralChanged(OnMineralChanged);
        EventManager.Unsubscribe_ModulePointChanged(OnModulePointChanged);
        EventManager.Unsubscribe_PvpPointChanged(OnPvpPointChanged);
        EventManager.Unsubscribe_InvestedMineralChanged(OnInvestedMineralChanged);
    }

    // UITabShip 등 3D 뷰포트 오른쪽을 잠식하는 패널이 열리면 카메라 viewport가 좁아짐 — 그 우측 경계에 맞춰 항상 "뷰포트 우상단"을 유지
    private void OnCameraViewportChanged(float ratio)
    {
        RepositionForViewport();
    }

    private void RepositionForViewport()
    {
        if (m_rectTransform == null) return;

        CameraController cam = CameraController.Instance;
        if (cam == null) return;

        float viewportRight = cam.GetViewportWidth();
        Vector2 anchorMin = m_rectTransform.anchorMin;
        Vector2 anchorMax = m_rectTransform.anchorMax;
        anchorMin.x = viewportRight;
        anchorMax.x = viewportRight;
        m_rectTransform.anchorMin = anchorMin;
        m_rectTransform.anchorMax = anchorMax;
    }

    private void OnResetMineralInvestedButtonClicked()
    {
        SoundManager.Instance.PlayFX(EFx.Button_Clicked, retrigger: true);
        if (m_displayedInvestedMineral <= 0) return;

        var loc = LocalizationManager.Instance;
        UIManager.Instance.ShowConfirmPopup(new ConfirmPopupConfig
        {
            title        = loc.Get("UIPopupMessage_ResetAllInvestedMineralTitle"),
            message      = loc.Get("UIPopupMessage_ResetAllInvestedMineralMessage"),
            refundAmount = m_displayedInvestedMineral,
            onConfirm    = SendResetAllInvestedMineral,
            onCancel     = () => { },
        });
    }

    private void SendResetAllInvestedMineral()
    {
        SpaceFleet playerFleet = ObjectManager.Instance.GetMyFleet();
        if (playerFleet == null || playerFleet.m_fleetInfo == null) return;

        var request = new FleetResetAllInvestedMineralRequest { fleetId = playerFleet.m_fleetInfo.id };
        NetworkManager.Instance.FleetResetAllInvestedMineral(request, OnResetAllInvestedMineralResponse);
    }

    private void OnResetAllInvestedMineralResponse(ApiResponse<FleetResetAllInvestedMineralResponse> response)
    {
        if (response == null || response.errorCode != 0)
        {
            Debug.LogWarning($"[UIResourceBar] FleetResetAllInvestedMineral 실패: {ErrorCodeMapping.GetMessage(response != null ? response.errorCode : 0)}");
            return;
        }

        Commander commander = DataManager.Instance.m_currentCommander;
        if (commander != null)
            commander.UpdateMineral(response.data.mineralRemain);

        SpaceFleet playerFleet = ObjectManager.Instance.GetMyFleet();
        if (playerFleet != null && response.data.updatedFleetInfo != null)
            playerFleet.ApplyMineralReset(response.data.updatedFleetInfo);
    }

    // 최초 1회 직접 갱신 — 애니메이션 없음
    private void InitAll(Commander commander)
    {
        var info = commander.GetInfo();
        if (info == null) return;

        m_displayedMineral     = commander.GetMineral();
        m_displayedModulePoint = commander.GetModulePoint();
        m_displayedPvpPoint    = commander.GetPvpPoint();

        if (m_textMineralCurrent != null)     m_textMineralCurrent.text     = CommonUtility.FormatNumber(m_displayedMineral);
        if (m_textModulePointCurrent != null) m_textModulePointCurrent.text = CommonUtility.FormatNumber(m_displayedModulePoint);
        if (m_textModulePointMaxGot != null)  m_textModulePointMaxGot.text  = $"/{CommonUtility.FormatNumber(commander.GetModulePointMaxGot())}";
        if (m_textPvpPointCurrent != null)    m_textPvpPointCurrent.text    = CommonUtility.FormatNumber(m_displayedPvpPoint);
        if (m_textPvpPointMaxGot != null)     m_textPvpPointMaxGot.text     = $"/{CommonUtility.FormatNumber(commander.GetPvpPointMaxGot())}";

        if (m_textMineralInvested != null)
        {
            SpaceFleet playerFleet = ObjectManager.Instance.GetMyFleet();
            m_displayedInvestedMineral = playerFleet != null ? playerFleet.GetTotalInvestedMineral() : 0;
            RefreshInvestedMineralUI(m_displayedInvestedMineral, m_displayedMineral);
        }

        TryParseExpiry(info.pvpPointExpiry, out m_pvpExpiry);

        bool hasPvp = m_pvpExpiry != default && m_pvpExpiry.ToUniversalTime() > DateTime.UtcNow;
        if (m_imagePvpPointDday != null)
        {
            m_imagePvpPointDday.gameObject.SetActive(hasPvp);
            m_textPvpPointDday.gameObject.SetActive(hasPvp);
        }

        if (m_ddayCoroutine != null) StopCoroutine(m_ddayCoroutine);
        if (hasPvp == true)
            m_ddayCoroutine = StartCoroutine(RunDdayUpdate());

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
    }

    public void OnMineralChanged(long mineral)
    {
        StartFieldAnimation(ref m_coroutineMineral, m_textMineralCurrent, m_displayedMineral, mineral);
        m_displayedMineral = mineral;
        RefreshInvestedMineralColor(m_displayedInvestedMineral, mineral);
    }

    private void OnInvestedMineralChanged(int totalInvested)
    {
        m_displayedInvestedMineral = totalInvested;
        RefreshInvestedMineralUI(totalInvested, m_displayedMineral);
    }

    private void RefreshInvestedMineralUI(int totalInvested, long currentMineral)
    {
        if (m_textMineralInvested == null) return;

        m_textMineralInvested.text = CommonUtility.FormatNumber(totalInvested);

        RefreshInvestedMineralColor(totalInvested, currentMineral);
    }

    private void RefreshInvestedMineralColor(int totalInvested, long currentMineral)
    {
        if (m_imageMineralInvested == null || m_textMineralInvested == null) return;

        if (totalInvested <= 0)
        {
            Color emptyColor = CommonUtility.PaletteColor("State.Disabled");
            m_imageMineralInvested.color = emptyColor;
            m_textMineralInvested.color  = emptyColor;
            return;
        }

        bool isInsufficient = currentMineral < totalInvested;
        Color mineralColor  = CommonUtility.PaletteColor("Mineral");

        m_imageMineralInvested.color = mineralColor;
        m_textMineralInvested.color  = isInsufficient == true ? s_colorInvestedInsufficient : mineralColor;
    }

    private void OnModulePointChanged(int modulePoint)
    {
        StartFieldAnimation(ref m_coroutineModulePoint, m_textModulePointCurrent, m_displayedModulePoint, modulePoint);
        m_displayedModulePoint = modulePoint;

        var commander = DataManager.Instance.m_currentCommander;
        if (commander != null && m_textModulePointMaxGot != null)
            m_textModulePointMaxGot.text = $"/ {CommonUtility.FormatNumber(commander.GetModulePointMaxGot())}";
    }

    private void OnPvpPointChanged(int pvpPoint)
    {
        StartFieldAnimation(ref m_coroutinePvpPoint, m_textPvpPointCurrent, m_displayedPvpPoint, pvpPoint);
        m_displayedPvpPoint = pvpPoint;

        var commander = DataManager.Instance.m_currentCommander;
        if (commander != null && m_textPvpPointMaxGot != null)
            m_textPvpPointMaxGot.text = $"/ {CommonUtility.FormatNumber(commander.GetPvpPointMaxGot())}";
    }

    private void StartFieldAnimation(ref Coroutine handle, TMP_Text textUI, long from, long to)
    {
        if (textUI == null) return;
        if (handle != null) StopCoroutine(handle);
        handle = StartCoroutine(AnimateCounter(textUI, from, to));
    }

    // from → to 카운팅 (변화량 * 0.03초, 최대 0.5초)
    private IEnumerator AnimateCounter(TMP_Text textUI, long from, long to)
    {
        if (from < 0 || from == to)
        {
            textUI.text = CommonUtility.FormatNumber(to);
            yield break;
        }

        float duration = Mathf.Min(Mathf.Abs(to - from) * 0.03f, 0.5f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t       = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            long  current = from + (long)((to - from) * t);
            textUI.text = CommonUtility.FormatNumber(current);
            yield return null;
        }

        textUI.text = CommonUtility.FormatNumber(to);
    }

    private IEnumerator RunDdayUpdate()
    {
        while (true)
        {
            if (m_pvpExpiry != default)
            {
                TimeSpan left = m_pvpExpiry.ToUniversalTime() - DateTime.UtcNow;
                if (left.TotalSeconds <= 0)
                {
                    if (m_imagePvpPointDday != null) m_imagePvpPointDday.gameObject.SetActive(false);
                    if (m_textPvpPointDday  != null) m_textPvpPointDday.gameObject.SetActive(false);
                    m_pvpExpiry = default;
                    yield break;
                }

                if (m_textPvpPointDday != null)
                {
                    m_textPvpPointDday.text = FormatTimeLeft(m_pvpExpiry);
                    if (left.TotalDays < 3)
                    {
                        float t = (Mathf.Sin(Time.time * Mathf.PI * 2f) + 1f) * 0.5f;
                        m_textPvpPointDday.color = Color.Lerp(m_pvpDdayColorBase, m_pvpDdayColorBright, t);
                        yield return s_waitFlicker;
                        continue;
                    }
                    else
                    {
                        m_textPvpPointDday.color = m_pvpDdayColorBase;
                    }
                }
            }
            else
            {
                yield break;
            }

            yield return s_wait1Sec;
        }
    }

    private static bool TryParseExpiry(string iso8601, out DateTime result)
    {
        if (string.IsNullOrEmpty(iso8601) == false)
        {
            if (DateTime.TryParse(iso8601, null, DateTimeStyles.RoundtripKind, out result))
                return true;
        }
        result = default;
        return false;
    }

    private static string FormatTimeLeft(DateTime expireAt)
    {
        TimeSpan left = expireAt.ToUniversalTime() - DateTime.UtcNow;
        if (left.TotalSeconds <= 0)  return "0s";
        if (left.TotalDays >= 30)    return $"{(int)(left.TotalDays / 30)}M";
        if (left.TotalDays >= 1)     return $"{(int)left.TotalDays}D";
        if (left.TotalHours >= 1)    return $"{(int)left.TotalHours}h";
        if (left.TotalMinutes >= 1)  return $"{(int)left.TotalMinutes}m";
        return $"{(int)left.TotalSeconds}s";
    }
}
