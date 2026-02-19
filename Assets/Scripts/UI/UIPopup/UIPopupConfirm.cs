// 확인/취소 팝업: left=상황 정보(업그레이드/언락/pvp 등), right=비용 정보
// CostStruct null 또는 rowLabels null 시 해당 컨테이너 자동 숨김
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIPopupConfirm : UIPopupBase
{
    [Header("Confirm Popup UI")]
    public TMP_Text titleText;
    public TMP_Text messageText;
    [SerializeField] private RowLabelValue m_rowPrefab;
    [SerializeField] private RectTransform m_detailcontainer;
    [SerializeField] private Transform m_rowContainerLeft;
    [SerializeField] private Transform m_rowContainerCenter;
    [SerializeField] private Transform m_rowContainerRight;
    public Button confirmButton;
    public Button cancelButton;

    private Action onCancelCallback;
    private Action onConfirmCallback;

    // left/right 풀 분리 (컨테이너 부모 혼입 방지)
    private List<RowLabelValue> m_activeRowsLeft = new List<RowLabelValue>();
    private List<RowLabelValue> m_pooledRowsLeft = new List<RowLabelValue>();
    private List<RowLabelValue> m_activeRowsRight = new List<RowLabelValue>();
    private List<RowLabelValue> m_pooledRowsRight = new List<RowLabelValue>();

    [SerializeField] private float m_detailHalfWidth = 440f;

    protected override void Awake()
    {
        base.Awake();
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClicked);
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirmClicked);
    }

    // rowLabels/rowValues: left 상황 정보 (null이면 left 컨테이너 숨김)
    // cost: right 비용 정보 (null 또는 전부 0이면 right 컨테이너 숨김)
    public void ShowPopupConfirm(string title, string message, string[] rowLabels, string[] rowValues, CostStruct cost, Action onConfirm, Action onCancel = null)
    {
        if (titleText != null) titleText.text = title;
        if (messageText != null) messageText.text = message;

        ClearRows();

        // Left: 상황 정보
        bool hasLeft = rowLabels != null && rowValues != null && rowLabels.Length > 0;
        if (m_rowContainerLeft != null) m_rowContainerLeft.gameObject.SetActive(hasLeft);
        if (hasLeft == true)
        {
            int count = Mathf.Min(rowLabels.Length, rowValues.Length);
            for (int i = 0; i < count; i++)
                AddRow(rowLabels[i], rowValues[i], m_rowContainerLeft, m_activeRowsLeft, m_pooledRowsLeft);
        }

        // Right: 비용 정보
        bool hasCost = cost != null && (cost.techLevel > 0 || cost.mineral > 0 || cost.mineralRare > 0 || cost.mineralExotic > 0 || cost.mineralDark > 0);
        if (m_rowContainerRight != null) m_rowContainerRight.gameObject.SetActive(hasCost);
        if (hasCost == true)
        {
            if (cost.techLevel > 0) AddRow("tech_level", cost.techLevel.ToString("N0"), m_rowContainerRight, m_activeRowsRight, m_pooledRowsRight);
            if (cost.mineral > 0) AddRow("mineral_amount", CommonUtility.FormatBigNumber(cost.mineral), m_rowContainerRight, m_activeRowsRight, m_pooledRowsRight);
            if (cost.mineralRare > 0) AddRow("mineral_rare_amount", CommonUtility.FormatBigNumber(cost.mineralRare), m_rowContainerRight, m_activeRowsRight, m_pooledRowsRight);
            if (cost.mineralExotic > 0) AddRow("mineral_exotic_amount", CommonUtility.FormatBigNumber(cost.mineralExotic), m_rowContainerRight, m_activeRowsRight, m_pooledRowsRight);
            if (cost.mineralDark > 0) AddRow("mineral_dark_amount", CommonUtility.FormatBigNumber(cost.mineralDark), m_rowContainerRight, m_activeRowsRight, m_pooledRowsRight);
        }

        UpdateDetailContainerLayout(hasLeft, hasCost);

        onCancelCallback = onCancel;
        onConfirmCallback = onConfirm;

        base.ShowPopup();
    }

    // detailcontainer 너비 제어: 양쪽 모두 = 전체, 한쪽만 = 절반, 둘 다 없음 = 숨김
    private void UpdateDetailContainerLayout(bool hasLeft, bool hasCost)
    {
        if (m_detailcontainer == null) return;

        bool either = hasLeft || hasCost;
        m_detailcontainer.gameObject.SetActive(either);
        if (either == false) return;

        bool both = hasLeft && hasCost;
        if (m_rowContainerCenter != null)
            m_rowContainerCenter.gameObject.SetActive(both);
    }

    private void AddRow(string labelKey, string value, Transform container, List<RowLabelValue> activeList, List<RowLabelValue> pool)
    {
        RowLabelValue row = GetOrCreateRow(container, pool);
        row.SetRow(labelKey, value);
        row.gameObject.SetActive(true);
        activeList.Add(row);
    }

    private RowLabelValue GetOrCreateRow(Transform container, List<RowLabelValue> pool)
    {
        if (pool.Count > 0)
        {
            var row = pool[pool.Count - 1];
            pool.RemoveAt(pool.Count - 1);
            return row;
        }
        return Instantiate(m_rowPrefab, container);
    }

    private void ClearRows()
    {
        foreach (var row in m_activeRowsLeft)
        {
            row.gameObject.SetActive(false);
            m_pooledRowsLeft.Add(row);
        }
        m_activeRowsLeft.Clear();

        foreach (var row in m_activeRowsRight)
        {
            row.gameObject.SetActive(false);
            m_pooledRowsRight.Add(row);
        }
        m_activeRowsRight.Clear();
    }

    private void OnConfirmClicked()
    {
        onConfirmCallback?.Invoke();
    }

    private void OnCancelClicked()
    {
        onCancelCallback?.Invoke();
    }

    private void OnDestroy()
    {
        if (cancelButton != null) cancelButton.onClick.RemoveAllListeners();
        if (confirmButton != null) confirmButton.onClick.RemoveAllListeners();
    }
}
