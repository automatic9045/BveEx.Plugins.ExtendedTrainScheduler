using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using BveTypes.ClassWrappers;
using BveTypes.ClassWrappers.Extensions;
using FastMember;
using ObjectiveHarmonyPatch;
using TypeWrapping;

using BveEx.Extensions.MapStatements;
using BveEx.Extensions.PreTrainPatch;
using BveEx.PluginHost;
using BveEx.PluginHost.Plugins;
using BveEx.PluginHost.Plugins.Extensions;

using Automatic9045.BveEx.ExtendedTrainScheduler.PreTrains;
using Automatic9045.BveEx.ExtendedTrainScheduler.Speed;
using Automatic9045.BveEx.ExtendedTrainScheduler.Tracks;

namespace Automatic9045.BveEx.ExtendedTrainScheduler
{
    [Plugin(PluginType.MapPlugin)]
    public class PluginMain : AssemblyPluginBase
    {
        private readonly HarmonyPatch HarmonyPatch;

        private bool AreOperatorsInitialized = false;
        private TrackOperator TrackOperator;
        private PreTrainOperator PreTrainOperator;
        private SpeedOperator SpeedOperator;
        private PreTrainPatch PreTrainPatch;

        public PluginMain(PluginBuilder builder) : base(builder)
        {
            ClassMemberSet trainMembers = BveHacker.BveTypes.GetClassInfoOf<Train>();
            FastMethod compileToSchedulesMethod = trainMembers.GetSourceMethodOf(nameof(Train.CompileToSchedules));
            HarmonyPatch = HarmonyPatch.Patch(nameof(ExtendedTrainScheduler), compileToSchedulesMethod.Source, PatchType.Prefix);
            HarmonyPatch.Invoked += (sender, e) =>
            {
                Train instance = Train.FromSource(e.Instance);

                if (!AreOperatorsInitialized)
                {
                    StatementSet statements = StatementSet.Load(Extensions.GetExtension<IStatementSet>());
                    WrappedSortedList<string, TrainInfo> trainInfos = instance.Map.TrainInfos;

                    TrackOperator = TrackOperator.Create(statements.SetTrack, trainInfos, ThrowError);
                    PreTrainOperator = PreTrainOperator.Create(statements.AttachToTrain, statements.Detach, trainInfos, ThrowError);
                    SpeedOperator = SpeedOperator.Create(statements.StopUntil,statements.StopAtUntil, statements.StopAt, statements.AccelerateToHereAt,BveHacker.MapLoader.Map.TrainInfos, ThrowError);
                    SpeedOverrider.Override(statements.AccelerateFromHere, statements.AccelerateToHere, BveHacker.MapLoader.Map.TrainInfos, ThrowError, ((Station)instance.Map.Stations[0]).DefaultTime);

                    AreOperatorsInitialized = true;


                    void ThrowError(string message, string senderName, int lineIndex, int charIndex)
                    {
                        BveHacker.LoadingProgressForm.ThrowError(message, senderName ?? Name, lineIndex, charIndex);
                        Application.DoEvents();
                    }
                }

                IEnumerable<TrainSchedule> stopSchedules = SpeedOperator.CompileToSchedules(instance.TrainInfo, ((Station)instance.Map.Stations[0]).DefaultTime);
                foreach (TrainSchedule schedule in stopSchedules)
                {
                    instance.Schedules.Add(schedule);
                }

                return new PatchInvokationResult(SkipModes.SkipPatches | SkipModes.SkipOriginal);
            };

            BveHacker.ScenarioCreated += OnScenarioCreated;
            BveHacker.ScenarioClosed += OnScenarioClosed;
        }

        public override void Dispose()
        {
            HarmonyPatch.Dispose();
            PreTrainPatch?.Dispose();
            BveHacker.ScenarioCreated -= OnScenarioCreated;
            BveHacker.ScenarioClosed -= OnScenarioClosed;
        }

        private void OnScenarioCreated(ScenarioCreatedEventArgs e)
        {
            if (!AreOperatorsInitialized) return;

            PreTrainOperator.SectionManager = e.Scenario.SectionManager;
            PreTrainOperator.Trains = e.Scenario.Trains;

            PreTrainPatch = Extensions.GetExtension<IPreTrainPatchFactory>().Patch(nameof(ExtendedTrainScheduler), e.Scenario.SectionManager, PreTrainOperator);
        }

        private void OnScenarioClosed(EventArgs e)
        {
            AreOperatorsInitialized = false;

            TrackOperator = null;
            PreTrainOperator = null;
        }

        public override void Tick(TimeSpan elapsed)
        {
            if (!AreOperatorsInitialized) return;

            WrappedSortedList<string, Train> trains = BveHacker.Scenario.Trains;
            TrackOperator.Tick(trains);
        }
    }
}
