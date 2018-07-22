using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

namespace VRCSDK2
{
    public static class AvatarValidation
    {
        public static readonly string[] ComponentTypeWhiteList = new string[] {
            "UnityEngine.Transform",
            "UnityEngine.Animator",
            "VRC.Core.PipelineManager",
#if !VRC_CLIENT
            "VRC.Core.PipelineSaver",
#endif
            "VRCSDK2.VRC_AvatarDescriptor",
            "NetworkMetadata",
            "RootMotion.FinalIK.IKExecutionOrder",
            "RootMotion.FinalIK.VRIK",
            "RootMotion.FinalIK.FullBodyBipedIK",
            "RootMotion.FinalIK.LimbIK",
            "RootMotion.FinalIK.AimIK",
            "RootMotion.FinalIK.BipedIK",
            "RootMotion.FinalIK.GrounderIK",
            "RootMotion.FinalIK.GrounderFBBIK",
            "RootMotion.FinalIK.GrounderVRIK",
            "RootMotion.FinalIK.GrounderQuadruped",
            "RootMotion.FinalIK.TwistRelaxer",
            "RootMotion.FinalIK.ShoulderRotator",
            "RootMotion.FinalIK.FBBIKArmBending",
            "RootMotion.FinalIK.FBBIKHeadEffector",
            "RootMotion.FinalIK.FABRIK",
            "RootMotion.FinalIK.FABRIKChain",
            "RootMotion.FinalIK.FABRIKRoot",
            "RootMotion.FinalIK.CCDIK",
            "RootMotion.FinalIK.RotationLimit",
            "RootMotion.FinalIK.RotationLimitHinge",
            "RootMotion.FinalIK.RotationLimitPolygonal",
            "RootMotion.FinalIK.RotationLimitSpline",
            "UnityEngine.SkinnedMeshRenderer",
            "LimbIK", // our limbik based on Unity ik
            "AvatarAnimation",
            "LoadingAvatarTextureAnimation",
            "UnityEngine.MeshFilter",
            "UnityEngine.MeshRenderer",
            "UnityEngine.Animation",
            "UnityEngine.ParticleSystem",
            "UnityEngine.ParticleSystemRenderer",
            "DynamicBone",
            "DynamicBoneCollider",
            "UnityEngine.TrailRenderer",
            "UnityEngine.Cloth",
            "UnityEngine.Light",
            "UnityEngine.Collider",
            "UnityEngine.Rigidbody",
            "UnityEngine.Joint",
            "UnityEngine.Camera",
            "UnityEngine.FlareLayer",
            "UnityEngine.GUILayer",
            "UnityEngine.AudioSource",
            "UnityEngine.EllipsoidParticleEmitter",
            "UnityEngine.ParticleRenderer",
            "UnityEngine.ParticleAnimator",
            "UnityEngine.MeshParticleEmitter",
            "UnityEngine.LineRenderer"
        };

        private static IEnumerable<System.Type> FindTypes()
        {
            Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();

            return ComponentTypeWhiteList.Select((name) =>
            {
                foreach (Assembly a in assemblies)
                {
                    System.Type found = a.GetType(name);
                    if (found != null)
                        return found;
                }

                //This is really verbose for some SDK scenes, eg.
                //If they don't have FinalIK installed
#if VRC_CLIENT && UNITY_EDITOR
                Debug.LogError("Could not find type " + name);
#endif
                return null;
            }).Where(t => t != null);
        }

        public static IEnumerable<Component> FindIllegalComponents(string Name, GameObject currentAvatar)
        {
            HashSet<System.Type> typesInUse = new HashSet<System.Type>();
            List<Component> componentsInUse = new List<Component>();
            Queue<GameObject> children = new Queue<GameObject>();
            children.Enqueue(currentAvatar.gameObject);
            while (children.Count > 0)
            {
                GameObject child = children.Dequeue();
                int childCount = child.transform.childCount;
                for (int idx = 0; idx < child.transform.childCount; ++idx)
                    children.Enqueue(child.transform.GetChild(idx).gameObject);
                foreach (Component c in child.transform.GetComponents<Component>())
                {
                    if (c == null)
                        continue;

                    if (typesInUse.Contains(c.GetType()) == false)
                        typesInUse.Add(c.GetType());
                    componentsInUse.Add(c);
                }
            }

            IEnumerable<System.Type> foundTypes = FindTypes();
            return componentsInUse.Where(c => !foundTypes.Any(allowedType => c != null && (c.GetType() == allowedType || c.GetType().IsSubclassOf(allowedType))));
        }

        public static void RemoveIllegalComponents(string Name, GameObject currentAvatar, bool retry = true)
        {
            IEnumerable<Component> componentsToRemove = VRCSDK2.AvatarValidation.FindIllegalComponents(Name, currentAvatar);

            HashSet<string> componentsToRemoveNames = new HashSet<string>();
            foreach (Component c in componentsToRemove)
            {
                if (componentsToRemoveNames.Contains(c.GetType().Name) == false)
                    componentsToRemoveNames.Add(c.GetType().Name);
                Object.DestroyImmediate(c);
            }

            if (retry && componentsToRemoveNames.Count > 0)
            {
                Debug.LogErrorFormat("Avatar {0} had components of the following types removed: {1}", Name, string.Join(", ", componentsToRemoveNames.ToArray()));

                // Call again, to see if there's components that were prevented from being removed
                RemoveIllegalComponents(Name, currentAvatar, false);
            }
        }

        public static List<AudioSource> EnforceAudioSourceLimits(GameObject currentAvatar)
        {
            List<AudioSource> audioSources = new List<AudioSource>();

            Queue<GameObject> children = new Queue<GameObject>();
            children.Enqueue(currentAvatar.gameObject);
            while (children.Count > 0)
            {
                GameObject child = children.Dequeue();
                int childCount = child.transform.childCount;
                for (int idx = 0; idx < child.transform.childCount; ++idx)
                    children.Enqueue(child.transform.GetChild(idx).gameObject);

#if VRC_CLIENT
                if (child.GetComponent<USpeaker>() != null)
                    continue;
#endif

                foreach (AudioSource au in child.transform.GetComponents<AudioSource>())
                {
                    if (au.volume > 0.9f)
                        au.volume = 0.9f;

                    au.spatialize = true;
                    au.bypassEffects = false;
                    au.bypassListenerEffects = false;
                    au.spatialBlend = 1f;
                    au.priority = Mathf.Clamp(au.priority, 200, 255);
                    au.spread = 0;
                    au.minDistance = Mathf.Clamp(au.minDistance, 0, 2);
                    au.maxDistance = Mathf.Clamp(au.maxDistance, 0, 30);

                    // setup a custom rolloff curve
                    AnimationCurve curve = new AnimationCurve(
                            new Keyframe(0, 1),
                            new Keyframe(au.minDistance/au.maxDistance, 1, 0, -10),
                            new Keyframe(0.077f , 0.5f, -5, -5),
                            new Keyframe(0.170f, 0.226f, -1.4f, -1.4f),
                            new Keyframe(0.333f, 0.102f, -0.5f, -0.5f),
                            new Keyframe(0.667f, 0.025f, -0.1f, -0.1f),
                            new Keyframe(1, 0)
                            );
                    au.rolloffMode = AudioRolloffMode.Custom;
                    au.SetCustomCurve(AudioSourceCurveType.CustomRolloff, curve);

                    audioSources.Add(au);
                }
            }

            return audioSources;
        }
    }
}
