using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using BveTypes.ClassWrappers;

using BveEx.Extensions.MapStatements;

namespace Automatic9045.BveEx.ExtendedTrainScheduler.Speed
{
    internal static class SpeedOverrider
    {
        internal static readonly double StepSpeed = 1 / 3.6; // 1 [km/h]

        public static void Override(IEnumerable<Statement> accelerateFromHereStatements, IEnumerable<Statement> accelerateToHereStatements,
            IReadOnlyDictionary<string, TrainInfo> trainInfos, Action<string, string, int, int> onError,TimeSpan originTimes)
        {
            Validator validator = new Validator(onError);

            List<StatementWithTrainInfo> forwardTrainStatements = new List<StatementWithTrainInfo>();
            List<StatementWithTrainInfo> backwardTrainStatements = new List<StatementWithTrainInfo>();

            LoadStatements(accelerateFromHereStatements, StateType.FromHere);
            LoadStatements(accelerateToHereStatements, StateType.ToHere);
            //LoadStatements(accelerateToHereAtStatements, StateType.ToHereAt);
            forwardTrainStatements.Sort();
            backwardTrainStatements.Sort();

            foreach (StatementWithTrainInfo item in forwardTrainStatements)
            {
                InsertStops(item);
            }

            foreach (StatementWithTrainInfo item in Enumerable.Reverse(backwardTrainStatements))
            {
                InsertStops(item);
            }


            void LoadStatements(IEnumerable<Statement> statements, StateType stateType)
            {
                foreach (Statement statement in statements)
                {
                    MapStatement statementSource = statement.Source;
                    validator.CheckClauseLength(statementSource, 7);

                    (string trainKey, TrainInfo trainInfo) = validator.GetTrain(statementSource, trainInfos);
                    if (trainKey is null) continue;

                    StatementWithTrainInfo item = new StatementWithTrainInfo(statement, trainInfo, stateType);
                    switch (trainInfo.Direction)
                    {
                        case 1:
                            forwardTrainStatements.Add(item);
                            break;

                        case -1:
                            backwardTrainStatements.Add(item);
                            break;

                        default:
                            validator.ThrowError($"キー '{trainKey}' の他列車はサポートされません。", statementSource);
                            break;
                    }
                }
            }

            void InsertStops(StatementWithTrainInfo statement)
            {
                MapStatement statementSource = statement.Statement.Source;

                int direction = statement.TrainInfo.Direction;
                double initialLocation = statementSource.Location;

                TrainStopObject firstStop = new TrainStopObject(initialLocation, double.MaxValue, 0, double.MaxValue, 0);
                int firstIndex = InsertStop(firstStop);

                int prevIndex = firstIndex - direction;
                int nextIndex = firstIndex + direction;
                if (prevIndex < 0 || statement.TrainInfo.Count <= prevIndex || nextIndex < 0 || statement.TrainInfo.Count <= nextIndex)
                {
                    validator.ThrowError("このステートメントは 2 つの Train.Stop ステートメントの間に定義する必要があります。", statementSource);
                    return;
                }
                TrainStopObject prevStop = (TrainStopObject)statement.TrainInfo[prevIndex];

                double initialSpeed = prevStop.Speed;
                double targetSpeed = Convert.ToDouble(statementSource.Clauses[6].Args[0]) / 3.6;

                double acceleration = 0;
                switch (statement.StateType)
                {
                    case StateType.FromHere:
                    case StateType.ToHere:
                        {
                            acceleration= Convert.ToDouble(statementSource.Clauses[6].Args[1]) / 3.6;
                            break;
                        }
                    case StateType.ToHereAt:
                        {
                            TimeSpan time= originTimes+prevStop.StopTime;
                            break;
                        }
                }

                firstStop.Speed = initialSpeed;
                if (targetSpeed == initialSpeed) return;

                int sign = Math.Sign(targetSpeed - initialSpeed);
                double signedStepSpeed = StepSpeed * sign;

                if (Math.Sign(acceleration) != sign) acceleration = double.MaxValue * sign;

                double accelerateDistance = (targetSpeed - initialSpeed) * (targetSpeed + initialSpeed) / 2 / acceleration * direction;
                if (statement.StateType==StateType.ToHere||statement.StateType==StateType.ToHereAt)
                {
                    initialLocation -= accelerateDistance;
                    firstStop.Location = initialLocation;
                }

                double lastLocation = initialLocation + accelerateDistance;
                if (Math.Sign(prevStop.Location - lastLocation) != Math.Sign(firstStop.Location - lastLocation))
                {
                    validator.ThrowError("他のステートメントの加減速と干渉します。距離を離してください。", statementSource);
                    return;
                }

                for (int i = 1; Math.Sign(initialSpeed + signedStepSpeed * i - targetSpeed) != sign; i++)
                {
                    double location = initialLocation + signedStepSpeed / acceleration * i * (initialSpeed + signedStepSpeed / 2 * i) * direction;
                    double stopAcceleration = i % 20 == 0 ? double.MaxValue : 0;
                    TrainStopObject stop = new TrainStopObject(location, stopAcceleration, 0, stopAcceleration, initialSpeed + signedStepSpeed * i);
                    statement.TrainInfo.Insert(stop);
                }

                TrainStopObject lastStop = new TrainStopObject(lastLocation, double.MaxValue, 0, double.MaxValue, targetSpeed);
                statement.TrainInfo.Insert(lastStop);


                int InsertStop(TrainStopObject stop)
                {
                    statement.TrainInfo.Insert(stop);
                    return statement.TrainInfo.IndexOf(stop);
                }
            }
        }

        enum StateType
        {
            FromHere,
            ToHere,
            ToHereAt
        }

        private struct StatementWithTrainInfo : IComparable<StatementWithTrainInfo>
        {
            public Statement Statement { get; }
            public TrainInfo TrainInfo { get; }
            public StateType StateType { get; }

            public StatementWithTrainInfo(Statement statement, TrainInfo trainInfo, StateType stateType)
            {
                Statement = statement;
                TrainInfo = trainInfo;
                StateType = stateType;
            }

            public int CompareTo(StatementWithTrainInfo other)
            {
                return Math.Sign(Statement.Source.Location - other.Statement.Source.Location);
            }
        }
    }
}
