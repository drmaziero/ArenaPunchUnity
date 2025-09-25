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
        
#if NOT_SERVER
        private bool Initialized { get; set; } = false;
        public bool IsExpanding { get; set; } = false;
#else
        private NetworkVariable<bool> Initialized { get; set; } = new NetworkVariable<bool>(false);
        public NetworkVariable<bool> IsExpanding { get; private set; } = new NetworkVariable<bool>(false);
#endif
        
        private const string ExpandKey = "Expand";
        private const string ReleaseKey = "Release";

        private void Awake()
        {
#if NOT_SERVER
            Initialized = false;
            IsExpanding = false;
#else
            Initialized.Value = false;
            IsExpanding.Value = false;
#endif
            PuncherAnimator.SetBool(ExpandKey,false);
            PuncherAnimator.SetBool(ReleaseKey, false);
        }

        private void Update()
        {
#if  !NOT_SERVER
              if (!IsServer)
                return;

             if (Initialized.Value)
                return;

            Initialized.Value = true;
#else
            if (Initialized)
                return;

            Initialized = true;
#endif
           
            InvokeRepeating("ObstacleAction",TimeToInitActions, IntervalToDoActions);
        }

        private void ObstacleAction()
        {
            StartCoroutine(PerformAction());
        }

        private IEnumerator PerformAction()
        {
#if NOT_SERVER
            IsExpanding = true;
#else
            IsExpanding.Value = true;
#endif
            PuncherAnimator.SetBool(ExpandKey,true);
            PuncherAnimator.SetBool(ReleaseKey, false);
            yield return new WaitForSeconds(TimeToExpand);
#if NOT_SERVER
            IsExpanding = false;
#else
            IsExpanding.Value = false;
#endif
            yield return new WaitForSeconds(TimeToWaitRelease);
            PuncherAnimator.SetBool(ExpandKey,false);
            PuncherAnimator.SetBool(ReleaseKey, true);
            yield return new WaitForSeconds(TimeToRelease);
            PuncherAnimator.SetBool(ExpandKey,false);
            PuncherAnimator.SetBool(ReleaseKey, false);
        }
    }
}