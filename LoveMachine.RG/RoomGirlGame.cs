﻿using System.Collections;
using HarmonyLib;
using LoveMachine.Core;
using UnityEngine;

namespace LoveMachine.RG
{
    internal class RoomGirlGame : GameDescriptor
    {
        private Animator femaleAnimator;
        private MonoBehaviour hscene;

        private int AnimationId => Traverse.Create(hscene)
            .Property("CtrlFlag")
            .Property("NowAnimationInfo")
            .Property<int>("ID").Value;

        private string AnimationName => Traverse.Create(hscene)
            .Property("CtrlFlag")
            .Property("NowAnimationInfo")
            .Property<string>("NameAnimation").Value;

        public override int AnimationLayer => 0;

        protected override Dictionary<Bone, string> FemaleBoneNames => new Dictionary<Bone, string>
        {
            { Bone.Vagina, "cf_J_Kokan" },
            { Bone.RightHand, "cf_J_Hand_Index01_R" },
            { Bone.LeftHand, "cf_J_Hand_Index01_L" },
            { Bone.RightBreast, "cf_J_Mune_Nip01_R" },
            { Bone.LeftBreast, "cf_J_Mune_Nip01_L" },
            { Bone.Mouth, "cf_J_MouthCavity" },
            { Bone.RightFoot, "cf_J_Toes01_L" },
            { Bone.LeftFoot, "cf_J_Toes01_R" }
        };

        protected override int HeroineCount => 1;

        protected override int MaxHeroineCount => 1;

        protected override bool IsHardSex => false;

        protected override bool IsHSceneInterrupted => false;

        public override Animator GetFemaleAnimator(int girlIndex) => femaleAnimator;

        protected override Transform GetDickBase() => GameObject.Find("chaM_001/BodyTop/p_cf_anim/" +
            "cf_J_Root/cf_N_height/cf_J_Hips/cf_J_Kosi01/cf_J_Kosi02/cm_J_dan_s/cm_J_dan_top/" +
            "cm_J_dan_f_top/k_m_tamaC_00").transform;

        protected override GameObject GetFemaleRoot(int girlIndex) => GameObject.Find("chaF_001");

        protected override string GetPose(int girlIndex) => AnimationName
            + "." + AnimationId
            + "." + femaleAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash.ToString();

        protected override bool IsIdle(int girlIndex) => false;

        protected override bool IsOrgasming(int girlIndex) => Traverse.Create(hscene)
            .Property("CtrlFlag")
            .Property<bool>("NowOrgasm").Value;

        protected override IEnumerator UntilReady()
        {
            while(GetFemaleRoot(0) == null)
            {
                yield return new WaitForSeconds(5f);
            }
            femaleAnimator = GameObject.Find("chaF_001/BodyTop/p_cf_anim").GetComponent<Animator>();
        }

        public void StartH(MonoBehaviour hscene)
        {
            this.hscene = hscene;
            StartH();
        }
    }
}
