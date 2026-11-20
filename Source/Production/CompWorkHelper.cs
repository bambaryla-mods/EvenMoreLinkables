using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Hobbes.MoreLinkablesProduction
{
    public class CompProperties_WorkHelper : CompProperties
    {
        public float radius = 5f;
        public int tickInterval = 100;
        public int reductionAmt = 100;
        public string workIconPath = "UI/WorkReducer";
        public string boostIconPath = "UI/BoostWork";

        public CompProperties_WorkHelper()
        {
            compClass = typeof(CompWorkHelper);
        }
    }

    public class CompWorkHelper : ThingComp
    {
        public enum State
        {
            WORK,
            BOOST
        }

        public State state = State.WORK;

        protected CompPowerTrader compPower;

        private CompProperties_WorkHelper Props => (CompProperties_WorkHelper)props;

        private Texture2D workIcon;
        private Texture2D boostIcon;
        private IntVec3[] areaCache;
        private int ticksActive;

        public Texture2D WorkIcon
        {
            get
            {
                if (workIcon == null)
                {
                    workIcon = ContentFinder<Texture2D>.Get(Props.workIconPath);
                }
                return workIcon;
            }
        }

        public Texture2D BoostIcon
        {
            get
            {
                if (boostIcon == null)
                {
                    boostIcon = ContentFinder<Texture2D>.Get(Props.boostIconPath);
                }
                return boostIcon;
            }
        }

        private IntVec3[] Area
        {
            get
            {
                if (areaCache == null)
                {
                    areaCache = GenRadial.RadialCellsAround(parent.Position, Props.radius, false).ToArray();
                }
                return areaCache;
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            ticksActive++;
            if (compPower.PowerOn && ticksActive >= Props.tickInterval && state == State.WORK)
            {
                ticksActive = 0;
                DoWork();
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            compPower = parent.GetComp<CompPowerTrader>();
        }

        private void DoWork()
        {
            IEnumerable<UnfinishedThing> things = WorkTargets().TakeRandom(1);
            foreach (Thing t in things)
            {
                UnfinishedThing ut = (UnfinishedThing)t;
                float amt = Math.Max(0f, ut.workLeft - Props.reductionAmt * 60);
                string moteMsg = $"Work reduced\nto {Mathf.CeilToInt(amt / 60)}!";
                if (amt == 0) moteMsg = "Work Complete!";
                MoteMaker.ThrowText(ut.TrueCenter(), ut.MapHeld, moteMsg, 2.5f);
                ut.workLeft = amt;
            }
        }

        private List<UnfinishedThing> WorkTargets()
        {
            List<UnfinishedThing> targets = new List<UnfinishedThing>();
            for (int i = 0; i < Area.Length; i++)
            {
                if (!Area[i].InBounds(parent.Map))
                {
                    continue;
                }
                foreach (Thing t in Area[i].GetItems(parent.Map))
                {
                    if (t is UnfinishedThing ut && ut.workLeft > 0f) targets.Add(ut);
                }
            }
            return targets;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref state, "state");
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            Command_Action command_action = new Command_Action();
            State stateToSwitchTo = State.BOOST;
            if (state == State.WORK)
            {
                command_action.defaultLabel = "Switch to Boost";
                command_action.defaultDesc = "Switch modes to boost productivity of nearby workbenches.";
                command_action.icon = BoostIcon;
                stateToSwitchTo = State.BOOST;
            }
            else if (state == State.BOOST)
            {
                command_action.defaultLabel = "Switch to Work";
                command_action.defaultDesc = "Switch modes to reduce the work left on nearby, unfinished things.";
                command_action.icon = WorkIcon;
                stateToSwitchTo = State.WORK;
            }
            command_action.action = delegate
            {
                state = stateToSwitchTo;
            };
            command_action.activateSound = SoundDefOf.Tick_Tiny;
            yield return command_action;
        }

        public override string CompInspectStringExtra()
        {
            return $"Current mode: {((state == State.WORK) ? "Work" : "Boost")}";
        }
    }
}
