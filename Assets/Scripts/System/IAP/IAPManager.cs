using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;
using Unity.Services.Core;

public class IAPManager : MonoSingleton<IAPManager>
{
    public const string PRODUCT_VIP = "vip_month";

    private IProductService m_productService;
    private IPurchaseService m_purchaseService;

    private Action<bool, string> m_onVipPurchaseComplete;
    private PendingOrder m_pendingOrder;
    private DateTime? m_vipExpiry;          // UTC, null이면 VIP 아님

    protected override void OnInitialize()
    {
        InitializePurchasing();
    }

    private async void InitializePurchasing()
    {
        try
        {
            await UnityServices.InitializeAsync();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[IAPManager] UGS 초기화 실패: {e.Message}");
        }

        try
        {
            IStoreService storeService = UnityIAPServices.DefaultStore();
            storeService.OnStoreConnected += OnStoreConnected;
            storeService.OnStoreDisconnected += OnStoreDisconnected;
            await storeService.Connect();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[IAPManager] 스토어 연결 실패: {e.Message}");
        }
    }

    private void OnStoreConnected()
    {
        m_productService = UnityIAPServices.DefaultProduct();
        m_productService.OnProductsFetched += OnProductsFetched;
        m_productService.OnProductsFetchFailed += OnProductsFetchFailed;

        m_purchaseService = UnityIAPServices.DefaultPurchase();
        m_purchaseService.OnPurchasePending += OnPurchasePending;
        m_purchaseService.OnPurchaseConfirmed += OnPurchaseConfirmed;
        m_purchaseService.OnPurchaseFailed += OnPurchaseFailed;

        var productDefs = new List<ProductDefinition>
        {
            new ProductDefinition(PRODUCT_VIP, ProductType.Consumable)
        };
        m_productService.FetchProducts(productDefs);
    }

    private void OnStoreDisconnected(StoreConnectionFailureDescription desc)
    {
        Debug.LogWarning($"[IAPManager] 스토어 연결 끊김: {desc.Message}");
    }

    private void OnProductsFetched(List<Product> products)
    {
        Debug.Log("[IAPManager] IAP 초기화 완료");
    }

    private void OnProductsFetchFailed(ProductFetchFailed failed)
    {
        Debug.LogWarning($"[IAPManager] 상품 조회 실패: {failed.FailureReason}");
    }

    public bool IsStoreReady()
    {
        return m_purchaseService != null;
    }

    public bool IsVipActive()
    {
        if (m_vipExpiry == null) return false;
        return DateTime.UtcNow < m_vipExpiry.Value;
    }

    public int GetVipRemainingDays()
    {
        if (m_vipExpiry == null || IsVipActive() == false) return 0;
        return Mathf.Max(0, (int)(m_vipExpiry.Value - DateTime.UtcNow).TotalDays);
    }

    // 스토어에서 현지화된 가격 문자열 반환 (예: "$4.99", "₩6,500")
    public string GetVipLocalizedPrice()
    {
        if (m_productService == null) return string.Empty;
        Product product = m_productService.GetProductById(PRODUCT_VIP);
        if (product == null) return string.Empty;
        return product.metadata.localizedPriceString;
    }

    public int GetMonthRemainingDays()
    {
        var now = DateTime.UtcNow;
        var endOfMonth = new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month), 23, 59, 59, DateTimeKind.Utc);
        return Mathf.Max(0, (int)(endOfMonth - now).TotalDays);
    }

    public string GetVipMonthDisplay()
    {
        var now = DateTime.UtcNow;
        string localeCode = LocalizationManager.Instance != null ? LocalizationManager.Instance.GetCurrentLocaleCode() : "ko";
        return localeCode == "ko" ? $"{now.Month}월" : now.ToString("MMM");
    }

    public void SetVipExpiry(string isoExpiry)
    {
        if (string.IsNullOrEmpty(isoExpiry))
        {
            m_vipExpiry = null;
        }
        else if (DateTime.TryParse(isoExpiry, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
            m_vipExpiry = dt.ToUniversalTime();
        else
            m_vipExpiry = null;

        EventManager.TriggerVipStatusChanged();
    }

    public void ApplyVipStatus(VipStatusResponse data)
    {
        if (data == null) return;
        SetVipExpiry(data.isVip ? data.vipExpiry : null);
    }

    public void FetchVipStatus(Action onDone = null)
    {
        NetworkManager.Instance.GetVipStatus(response =>
        {
            if (response != null && response.errorCode == (int)ServerErrorCode.SUCCESS)
                ApplyVipStatus(response.data);
            onDone?.Invoke();
        });
    }

    public void PurchaseVip(Action<bool, string> onComplete)
    {
        if (m_purchaseService == null)
        {
            Debug.LogWarning("[IAPManager] Store 초기화 안 됨");
            onComplete?.Invoke(false, null);
            return;
        }
        Product product = m_productService.GetProductById(PRODUCT_VIP);
        if (product == null)
        {
            Debug.LogWarning("[IAPManager] VIP 상품을 찾을 수 없음");
            onComplete?.Invoke(false, null);
            return;
        }
        m_onVipPurchaseComplete = onComplete;
        m_purchaseService.PurchaseProduct(product);
    }

    // ── IPurchaseService 이벤트 핸들러 ──────────────────────────────────────

    private void OnPurchasePending(PendingOrder pendingOrder)
    {
        bool isVipProduct = false;
        var cartItems = pendingOrder.CartOrdered.Items();
        if (cartItems != null)
        {
            foreach (var item in cartItems)
            {
                if (item.Product.definition.id == PRODUCT_VIP)
                {
                    isVipProduct = true;
                    break;
                }
            }
        }

        if (isVipProduct == false) return;

        m_pendingOrder = pendingOrder;
        string receipt = pendingOrder.Info.Receipt;

#if UNITY_EDITOR
        Debug.Log("[IAPManager] 에디터 모드 — 서버 검증 생략, VIP 이번 달 말일 즉시 적용");
        var now = DateTime.UtcNow;
        var endOfMonth = new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month), 23, 59, 59, DateTimeKind.Utc);
        SetVipExpiry(endOfMonth.ToString("O"));
        m_purchaseService.ConfirmPurchase(m_pendingOrder);
        m_onVipPurchaseComplete?.Invoke(true, receipt);
        m_onVipPurchaseComplete = null;
        m_pendingOrder = null;
#else
    #if UNITY_ANDROID
        string platform = "GooglePlay";
    #elif UNITY_IOS
        string platform = "AppleAppStore";
    #endif
        var request = new VipPurchaseRequest { receipt = receipt, platform = platform };
        NetworkManager.Instance.PurchaseVip(request, response =>
        {
            if (response == null)
            {
                Debug.LogError("[IAPManager] PurchaseVip 서버 응답 null");
                m_onVipPurchaseComplete?.Invoke(false, null);
                m_onVipPurchaseComplete = null;
                m_pendingOrder = null;
                return;
            }
            string vipExpiry = response.data != null ? response.data.vipExpiry : "null";
            Debug.Log($"[IAPManager] PurchaseVip 응답 errorCode={response.errorCode} vipExpiry={vipExpiry}");
            bool ok = response.errorCode == (int)ServerErrorCode.SUCCESS;
            if (ok == true && response.data != null)
            {
                SetVipExpiry(response.data.vipExpiry);
                m_purchaseService.ConfirmPurchase(m_pendingOrder);
            }
            if (m_onVipPurchaseComplete != null)
                m_onVipPurchaseComplete.Invoke(ok, ok ? receipt : null);
            m_onVipPurchaseComplete = null;
            m_pendingOrder = null;
        });
#endif
    }

    private void OnPurchaseConfirmed(Order order)
    {
        Debug.Log("[IAPManager] 구매 확정 완료");
    }

    private void OnPurchaseFailed(FailedOrder failedOrder)
    {
        Debug.LogWarning($"[IAPManager] 구매 실패: {failedOrder.FailureReason} — {failedOrder.Details}");
        m_onVipPurchaseComplete?.Invoke(false, null);
        m_onVipPurchaseComplete = null;
        m_pendingOrder = null;
    }
}
