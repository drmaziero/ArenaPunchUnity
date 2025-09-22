using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Controllers
{
    public class ObstacleController : NetworkBehaviour
    {
        [field: SerializeField] 
        private float TimeToInitActions { get; set; } = 5.0f;
        [field: SerializeField] 
        private float IntervalToDoActions { get; set; } = 10.0f;
        [field: SerializeField]
        private float TimeToExpand { get; set; } = 0.5f;
        [field: SerializeField]
        private float TimeToRelease { get; set; } = 0.5f;
        [field: SerializeField]
        private Animator PuncherAnimator { get; set; }

        [field: SerializeField] 
        private float TimeToWaitRelease { get; set; } = 3.0f;
        private NetworkVariable<bool> Initialized { get; set; } = new NetworkVariable<bool>(false);
        public NetworkVariable<bool> IsExpanding { get; private set; } = new NetworkVariable<bool>(false);

        private const string ExpandKey = "Expand";
        private const string ReleaseKey = "Release";

        private void Awake()
        {
            Initialized.Value = false;
            IsExpanding.Value = false;
            PuncherAnimator.SetBool(ExpandKey,false);
            PuncherAnimator.SetBool(ReleaseKey, false);
        }

        private void Update()
        {
            if (!IsServer)
                return;

            if (Initialized.Value)
                return;

            Initialized.Value = true;
            InvokeRepeating("ObstacleAction",TimeToInitActions, IntervalToDoActions);
        }

        private void ObstacleAction()
        {
            StartCoroutine(PerformAction());
        }

        private IEnumerator PerformAction()
        {
            IsExpanding.Value = true;
            PuncherAnimator.SetBool(ExpandKey,true);
            PuncherAnimator.SetBool(ReleaseKey, false);
            yield return new WaitForSeconds(TimeToExpand);
            IsExpanding.Value = false;
            yield return new WaitForSeconds(TimeToWaitRelease);
            PuncherAnimator.SetBool(ExpandKey,false);
            PuncherAnimator.SetBool(ReleaseKey, true);
            yield return new WaitForSeconds(TimeToRelease);
            PuncherAnimator.SetBool(ExpandKey,false);
            PuncherAnimator.SetBool(ReleaseKey, false);
        }
    }
}