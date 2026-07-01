// 엔진 화염 메시(EngineFlame 셰이더)에 부착 — 아군/적 소속에 따라 색상 오버라이드 (MaterialPropertyBlock, 머티리얼 인스턴싱 없음)
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class EngineFlameColorizer : MonoBehaviour
{
    private static readonly int s_colorId = Shader.PropertyToID("_Color");
    private static readonly Color k_enemyColor = new Color(1f, 0.2f, 0.2f, 1f);

    private MeshRenderer m_renderer;
    private MaterialPropertyBlock m_propertyBlock;
    private Color m_defaultColor;
    private bool m_defaultColorCached;

    public void SetEnemyColor(bool isEnemy)
    {
        if (m_renderer == null)
            m_renderer = GetComponent<MeshRenderer>();
        if (m_propertyBlock == null)
            m_propertyBlock = new MaterialPropertyBlock();
        if (m_defaultColorCached == false)
        {
            m_defaultColor = m_renderer.sharedMaterial.GetColor(s_colorId);
            m_defaultColorCached = true;
        }

        Color targetColor = isEnemy == true ? k_enemyColor : m_defaultColor;

        m_renderer.GetPropertyBlock(m_propertyBlock);
        m_propertyBlock.SetColor(s_colorId, targetColor);
        m_renderer.SetPropertyBlock(m_propertyBlock);
    }
}
