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
        Debug.Log("[IAP][1] OnInitialize — InitializePurchasing 시작");
        InitializePurchasing();
    }

    private async void InitializePurchasing()
    {
        Debug.Log("[IAP][2] UnityServices.InitializeAsync 호출");
        try
        {
            await UnityServices.InitializeAsync();
            Debug.Log("[IAP][3] UnityServices 초기화 완료");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[IAP][3] UGS 초기화 실패: {e.Message}");
        }

        Debug.Log("[IAP][4] ConfigurationBuilder 생성, 상품 추가: " + PRODUCT_VIP);
        var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
        builder.AddProduct(PRODUCT_VIP, ProductType.Subscription);
        Debug.Log("[IAP][5] UnityPurchasing.Initialize 호출");
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
        Debug.Log($"[IAP][SetVipExpiry] isoExpiry={isoExpiry ?? "null"}");
        if (string.IsNullOrEmpty(isoExpiry))
        {
            m_vipExpiry = null;
        }
        else if (DateTime.TryParse(isoExpiry, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
            m_vipExpiry = dt.ToUniversalTime();
        else
            m_vipExpiry = null;

        Debug.Log($"[IAP][SetVipExpiry] m_vipExpiry={m_vipExpiry?.ToString("O") ?? "null"}, IsVipActive={IsVipActive()}");
        EventManager.TriggerVipStatusChanged();
    }

    public int GetDailyMineralAmount()      { return m_dailyMineralAmount; }
    public int GetMineralRewardMultiplier() { return m_mineralRewardMultiplier; }

    public void FetchVipStatus(Action onDone = null)
    {
        Debug.Log("[IAP][FetchVipStatus] 서버 요청 시작");
        NetworkManager.Instance.GetVipStatus(response =>
        {
            if (response == null)
            {
                Debug.LogWarning("[IAP][FetchVipStatus] response=null");
                onDone?.Invoke();
                return;
            }
            Debug.Log($"[IAP][FetchVipStatus] errorCode={response.errorCode}, data={response.data != null}");
            if (response.errorCode == (int)ServerErrorCode.SUCCESS && response.data != null)
            {
                m_dailyMineralAmount      = response.data.dailyMineralAmount;
                m_mineralRewardMultiplier = response.data.mineralRewardMultiplier;
                Debug.Log($"[IAP][FetchVipStatus] isVip={response.data.isVip}, expiry={response.data.vipExpiry}, daily={m_dailyMineralAmount}, multiplier={m_mineralRewardMultiplier}");
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
        Debug.Log($"[IAP][PurchaseVip] IsStoreReady={IsStoreReady()}");
        if (m_storeController == null)
        {
            Debug.LogWarning("[IAP][PurchaseVip] Store 초기화 안 됨");
            onComplete?.Invoke(false, null);
            return;
        }
        m_onVipPurchaseComplete = onComplete;
        Debug.Log("[IAP][PurchaseVip] InitiatePurchase 호출: " + PRODUCT_VIP);
        m_storeController.InitiatePurchase(PRODUCT_VIP);
    }

    // ── IDetailedStoreListener ──────────────────────────────────────────────

    public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
    {
        m_storeController = controller;
        m_storeExtensionProvider = extensions;
        Debug.Log("[IAP][6] OnInitialized — IAP 초기화 완료. 상품 수: " + controller.products.all.Length);
        foreach (var p in controller.products.all)
            Debug.Log($"[IAP][6]   상품: {p.definition.id}, availableToPurchase={p.availableToPurchase}");
    }

    public void OnInitializeFailed(InitializationFailureReason error)
    {
        Debug.LogWarning($"[IAP][6] OnInitializeFailed: {error}");
    }

    public void OnInitializeFailed(InitializationFailureReason error, string message)
    {
        Debug.LogWarning($"[IAP][6] OnInitializeFailed: {error} — {message}");
    }

    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
    {
        Debug.Log($"[IAP][ProcessPurchase] productId={args.purchasedProduct.definition.id}");
        if (args.purchasedProduct.definition.id == PRODUCT_VIP)
        {
            string receipt = args.purchasedProduct.receipt;
#if UNITY_EDITOR
            Debug.Log("[IAP][ProcessPurchase] 에디터 모드 — 서버 검증 생략, VIP 30일 즉시 적용");
            SetVipExpiry(DateTime.UtcNow.AddDays(30).ToString("O"));
            m_onVipPurchaseComplete?.Invoke(true, receipt);
            m_onVipPurchaseComplete = null;
#else
    #if UNITY_ANDROID
            string platform = "GooglePlay";
    #elif UNITY_IOS
            string platform = "AppleAppStore";
    #endif
            Debug.Log($"[IAP][ProcessPurchase] platform={platform}, 서버 검증 요청");
            var request = new VipPurchaseRequest { receipt = receipt, platform = platform };
            NetworkManager.Instance.PurchaseVip(request, response =>
            {
                bool ok = response != null && response.errorCode == (int)ServerErrorCode.SUCCESS;
                Debug.Log($"[IAP][ProcessPurchase] 서버 응답: ok={ok}, errorCode={response?.errorCode}");
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
        Debug.LogWarning($"[IAP][OnPurchaseFailed] {product.definition.id} — {failureReason}");
        m_onVipPurchaseComplete?.Invoke(false, null);
        m_onVipPurchaseComplete = null;
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription)
    {
        Debug.LogWarning($"[IAP][OnPurchaseFailed] {product.definition.id} — {failureDescription.message}");
        m_onVipPurchaseComplete?.Invoke(false, null);
        m_onVipPurchaseComplete = null;
    }
}
