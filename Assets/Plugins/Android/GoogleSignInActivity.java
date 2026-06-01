package com.fidforge.thefirst;

import android.app.Activity;
import android.content.Intent;
import android.os.Bundle;

import com.google.android.gms.auth.api.signin.GoogleSignIn;
import com.google.android.gms.auth.api.signin.GoogleSignInAccount;
import com.google.android.gms.auth.api.signin.GoogleSignInClient;
import com.google.android.gms.auth.api.signin.GoogleSignInOptions;
import com.google.android.gms.common.api.ApiException;
import com.google.android.gms.tasks.Task;

import com.unity3d.player.UnityPlayer;

public class GoogleSignInActivity extends Activity {
    private static final int RC_SIGN_IN = 9001;
    // MonoSingleton은 typeof(T).Name으로 GameObject 이름 생성 → "GoogleSignInBridge"
    private static final String CALLBACK_OBJECT = "GoogleSignInBridge";

    private GoogleSignInClient m_client;

    // C#(AndroidJavaClass.CallStatic)에서 호출하는 진입점
    public static void startSignIn(Activity activity, String webClientId) {
        Intent intent = new Intent(activity, GoogleSignInActivity.class);
        intent.putExtra("web_client_id", webClientId);
        activity.startActivity(intent);
    }

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        String webClientId = getIntent().getStringExtra("web_client_id");
        if (webClientId == null || webClientId.isEmpty()) {
            UnityPlayer.UnitySendMessage(CALLBACK_OBJECT, "OnSignInFailure", "no_client_id");
            finish();
            return;
        }

        GoogleSignInOptions gso = new GoogleSignInOptions.Builder(GoogleSignInOptions.DEFAULT_SIGN_IN)
            .requestIdToken(webClientId)
            .requestEmail()
            .build();

        m_client = GoogleSignIn.getClient(this, gso);
        // 매번 계정 선택창 표시를 위해 signOut 후 진행
        m_client.signOut().addOnCompleteListener(this, task ->
            startActivityForResult(m_client.getSignInIntent(), RC_SIGN_IN));
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode != RC_SIGN_IN) return;

        Task<GoogleSignInAccount> task = GoogleSignIn.getSignedInAccountFromIntent(data);
        try {
            GoogleSignInAccount account = task.getResult(ApiException.class);
            String idToken = (account != null) ? account.getIdToken() : null;
            if (idToken != null && !idToken.isEmpty()) {
                UnityPlayer.UnitySendMessage(CALLBACK_OBJECT, "OnSignInSuccess", idToken);
            } else {
                UnityPlayer.UnitySendMessage(CALLBACK_OBJECT, "OnSignInFailure", "no_token");
            }
        } catch (ApiException e) {
            // statusCode 10 = DEVELOPER_ERROR → Google Cloud에 Android 클라이언트 미등록
            UnityPlayer.UnitySendMessage(CALLBACK_OBJECT, "OnSignInFailure", "error_" + e.getStatusCode());
        }
        finish();
    }

    @Override
    public void onBackPressed() {
        super.onBackPressed();
        UnityPlayer.UnitySendMessage(CALLBACK_OBJECT, "OnSignInFailure", "cancelled");
        finish();
    }
}
