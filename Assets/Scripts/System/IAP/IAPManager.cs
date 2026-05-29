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
    private DateTime? m_vipExpiry;   // UTC, null이면 VIP 아님

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

    public void SetVipExpiry(string isoExpiry)
    {
        if (string.IsNullOrEmpty(isoExpiry))
        {
            m_vipExpiry = null;
            return;
        }
        if (DateTime.TryParse(isoExpiry, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
            m_vipExpiry = dt.ToUniversalTime();
        else
            m_vipExpiry = null;
    }

    public void FetchVipStatus(Action onDone = null)
    {
        NetworkManager.Instance.GetVipStatus(response =>
        {
            if (response != null && response.errorCode == (int)ServerErrorCode.SUCCESS && response.data != null)
                SetVipExpiry(response.data.isVip ? response.data.vipExpiry : null);
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
#if UNITY_ANDROID
            string platform = "GooglePlay";
#elif UNITY_IOS
            string platform = "AppleAppStore";
#else
            string platform = "GooglePlay";
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
