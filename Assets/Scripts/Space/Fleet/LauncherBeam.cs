//------------------------------------------------------------------------------
using UnityEngine;
using System.Collections;
using Unity.VisualScripting;

public class LauncherBeam : LauncherBase
{
    private ModuleData m_moduleData;
    private Vector3 m_slotScale = Vector3.one;

    private Color m_beamColor;

    private static readonly Color k_allyColor = Color.cyan;
    private static readonly Color k_enemyColor = new Color(1f, 0.2f, 0.2f); // 붉은색

    public void InitializeLauncherBeam(ModuleData moduleData, int firePointIndex, bool isEnemy, Vector3 slotScale)
    {
        if (m_isInitialized == true) return;

        m_beamColor = isEnemy ? k_enemyColor : k_allyColor;
        m_slotScale = slotScale;

        // 인덱스에 맞는 FirePoint 찾기
        m_firePoint = FindFirePointByIndex(firePointIndex);
        if (m_firePoint == null)
            m_firePoint = transform;
            

        if (m_audioSource == null)
        {
            m_audioSource = GetComponent<AudioSource>();
            if (m_audioSource == null)
            {
                m_audioSource = gameObject.AddComponent<AudioSource>();
                m_audioSource.playOnAwake = false;
            }
        }

        m_moduleData = moduleData;

        m_isInitialized = true;
    }

    public override void Fire(ModuleBase target, float damage, ModuleBase sourceModuleBase = null, Vector3 hitPoint = default)
    {
        if (m_isInitialized == false) return;
        StartCoroutine(FireBeamCoroutine(target, damage, sourceModuleBase, hitPoint));
    }

    private IEnumerator FireBeamCoroutine(ModuleBase target, float damage, ModuleBase sourceModuleBase, Vector3 hitPoint)
    {
        //ParticleSystem muzzleEffect = ObjectManager.Instance.m_poolManager.GetParticleSystem_Play_AutoReturn(EPoolName.EFFECT_BEAM_MUZZLE, m_firePoint);

        SoundManager.Instance.PlayFX(EFx.Beam_Fire1, m_firePoint.position);

        //yield return new WaitForSeconds(muzzleEffect.main.duration);
        if (target == null) yield break;

        ProjectileBeam beam = ObjectManager.Instance.m_poolManager.Get<ProjectileBeam>(EPoolName.PROJECTILE_BEAM);
        if (beam == null) yield break;
        beam.transform.position = m_firePoint.position;

        beam.InitializeProjectileBeam(m_firePoint, target, damage, m_moduleData, m_beamColor, sourceModuleBase, m_slotScale.x, hitPoint);
    }

}
