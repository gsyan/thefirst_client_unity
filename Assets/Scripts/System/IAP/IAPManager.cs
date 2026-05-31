using System;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;
using Unity.Services.Core;

public class IAPManager : MonoSingleton<IAPManager>, IDetailedStoreListener
{
    public const string PRODUCT_VIP = "vip_30day";

    private IStoreController m_storeController;
    private IExtensionProvider m_storeExtensionProvider;

    private Action<bool, string> m_onVipPurchaseComplete;
    private DateTime? m_vipExpiry;       // UTC, null이면 VIP 아님
    private int m_dailyMineralAmount;       // 서버 설정 일일 지급량
    private int m_mineralRewardMultiplier;  // 서버 설정 보상 배율

    protected override void OnInitialize()
    {
        // InitializePurchasing();
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

        var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
        builder.AddProduct(PRODUCT_VIP, ProductType.Subscription);
        UnityPurchasing.Initialize(this, builder);
    }

    public bool IsStoreReady()
    {
        return m_storeController != null;
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
        if (m_storeController == null) return string.Empty;
        var product = m_storeController.products.WithID(PRODUCT_VIP);
        if (product == null) return string.Empty;
        return product.metadata.localizedPriceString;
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

    public int GetDailyMineralAmount()      { return m_dailyMineralAmount; }
    public int GetMineralRewardMultiplier() { return m_mineralRewardMultiplier; }

    public void FetchVipStatus(Action onDone = null)
    {
        NetworkManager.Instance.GetVipStatus(response =>
        {
            if (response != null && response.errorCode == (int)ServerErrorCode.SUCCESS && response.data != null)
            {
                m_dailyMineralAmount      = response.data.dailyMineralAmount;
                m_mineralRewardMultiplier = response.data.mineralRewardMultiplier;
                SetVipExpiry(response.data.isVip ? response.data.vipExpiry : null);
            }
            onDone?.Invoke();
        });
    }

    public void TryClaimDailyMineral(Action<VipDailyMineralResponse> onResult)
    {
        if (IsVipActive() == false)
        {
            onResult?.Invoke(null);
            return;
        }
        NetworkManager.Instance.ClaimVipDailyMineral(response =>
        {
            if (response != null && response.errorCode == (int)ServerErrorCode.SUCCESS)
                onResult?.Invoke(response.data);
            else
                onResult?.Invoke(null);
        });
    }

    public void PurchaseVip(Action<bool, string> onComplete)
    {
        if (m_storeController == null)
        {
            Debug.LogWarning("[IAPManager] Store 초기화 안 됨");
            onComplete?.Invoke(false, null);
            return;
        }
        m_onVipPurchaseComplete = onComplete;
        m_storeController.InitiatePurchase(PRODUCT_VIP);
    }

    // ── IDetailedStoreListener ──────────────────────────────────────────────

    public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
    {
        m_storeController = controller;
        m_storeExtensionProvider = extensions;
        Debug.Log("[IAPManager] IAP 초기화 완료");
    }

    public void OnInitializeFailed(InitializationFailureReason error)
    {
        Debug.LogWarning($"[IAPManager] 초기화 실패: {error}");
    }

    public void OnInitializeFailed(InitializationFailureReason error, string message)
    {
        Debug.LogWarning($"[IAPManager] 초기화 실패: {error} — {message}");
    }

    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
    {
        if (args.purchasedProduct.definition.id == PRODUCT_VIP)
        {
            string receipt = args.purchasedProduct.receipt;
#if UNITY_EDITOR
            Debug.Log("[IAPManager] 에디터 모드 — 서버 검증 생략, VIP 30일 즉시 적용");
            SetVipExpiry(DateTime.UtcNow.AddDays(30).ToString("O"));
            m_onVipPurchaseComplete?.Invoke(true, receipt);
            m_onVipPurchaseComplete = null;
#else
    #if UNITY_ANDROID
            string platform = "GooglePlay";
    #elif UNITY_IOS
            string platform = "AppleAppStore";
    #endif
            var request = new VipPurchaseRequest { receipt = receipt, platform = platform };
            NetworkManager.Instance.PurchaseVip(request, response =>
            {
                bool ok = response != null && response.errorCode == (int)ServerErrorCode.SUCCESS;
                if (ok == true && response.data != null)
                    SetVipExpiry(response.data.vipExpiry);
                m_onVipPurchaseComplete?.Invoke(ok, ok ? receipt : null);
                m_onVipPurchaseComplete = null;
            });
#endif
        }
        return PurchaseProcessingResult.Complete;
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
    {
        Debug.LogWarning($"[IAPManager] 구매 실패: {product.definition.id} — {failureReason}");
        m_onVipPurchaseComplete?.Invoke(false, null);
        m_onVipPurchaseComplete = null;
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription)
    {
        Debug.LogWarning($"[IAPManager] 구매 실패: {product.definition.id} — {failureDescription.message}");
        m_onVipPurchaseComplete?.Invoke(false, null);
        m_onVipPurchaseComplete = null;
    }
}
