// 함대 진형 선택 팝업 — 목록에서 선택 후 Confirm 버튼으로 적용
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIPopupFormation : UIPopupBase
{
    [SerializeField] private RectTransform m_contentContainer;  // VerticalLayoutGroup
    [SerializeField] private GameObject m_formationItemPrefab;  // ScrollViewFormationItem 프리팹
    [SerializeField] private TMP_Text m_detailText;
    [SerializeField] private Button m_confirmButton;
    [SerializeField] private Button m_closeButton;

    private Action<EFormationType> m_onSelected;
    private EFormationType? m_selectedFormationType = null;

    private readonly List<ScrollViewFormationItem> m_pool = new();
    private readonly List<ScrollViewFormationItem> m_active = new();

    protected override void Awake()
    {
        base.Awake();
        if (m_confirmButton != null) m_confirmButton.onClick.AddListener(OnConfirmClicked);
        if (m_closeButton != null)   m_closeButton.onClick.AddListener(HidePopup);
    }

    public void ShowPopup(EFormationType currentFormationType, Action<EFormationType> onSelected)
    {
        m_onSelected = onSelected;
        m_selectedFormationType = currentFormationType;
        PopulateList();
        RefreshSelection();
        UpdateConfirmButton();
        base.ShowPopup();
    }

    private void PopulateList()
    {
        for (int i = 0; i < m_active.Count; i++)
            m_active[i].gameObject.SetActive(false);
        m_active.Clear();

        int poolIndex = 0;
        var formationTypes = Enum.GetValues(typeof(EFormationType));
        foreach (EFormationType formationType in formationTypes)
        {
            ScrollViewFormationItem item;
            if (poolIndex < m_pool.Count)
            {
                item = m_pool[poolIndex];
                item.gameObject.SetActive(true);
            }
            else
            {
                var go = Instantiate(m_formationItemPrefab, m_contentContainer);
                item = go.GetComponent<ScrollViewFormationItem>();
                m_pool.Add(item);
            }

            EFormationType captured = formationType;
            item.InitializeScrollViewFormationItem(
                () => OnItemClicked(captured),
                formationType.ToString()
            );
            m_active.Add(item);
            poolIndex++;
        }
    }

    private void OnItemClicked(EFormationType formationType)
    {
        m_selectedFormationType = formationType;
        RefreshSelection();
        UpdateConfirmButton();
    }

    private void RefreshSelection()
    {
        var formationTypes = Enum.GetValues(typeof(EFormationType));
        int index = 0;
        foreach (EFormationType formationType in formationTypes)
        {
            if (index < m_active.Count)
                m_active[index].SetSelected(m_selectedFormationType == formationType);
            index++;
        }
    }

    private void UpdateConfirmButton()
    {
        if (m_confirmButton != null)
            m_confirmButton.interactable = m_selectedFormationType.HasValue;
    }

    private void OnConfirmClicked()
    {
        if (m_selectedFormationType.HasValue == false) return;
        m_onSelected?.Invoke(m_selectedFormationType.Value);
        HidePopup();
    }
}
