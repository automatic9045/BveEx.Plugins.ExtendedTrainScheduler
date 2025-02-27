using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using BveTypes.ClassWrappers;

using BveEx.Extensions.MapStatements;

namespace Automatic9045.BveEx.ExtendedTrainScheduler.Speed
{
    internal class SpeedOperator
    {
        private readonly IReadOnlyDictionary<TrainStopObject, TimeSpan> DepartureTimes;
        private readonly IReadOnlyDictionary<TrainStopObject, TimeSpan>ArrivalTimes;
        private readonly Dictionary<TrainStopObject, Statement>ToHereTimes;
        private readonly Action<string, string, int, int> OnError;

        public SpeedOperator(IReadOnlyDictionary<TrainStopObject, TimeSpan> departureTimes, IReadOnlyDictionary<TrainStopObject, TimeSpan> arrivalTimes,Dictionary<TrainStopObject, Statement> toHereStatement,Action<string, string, int, int> onError)
        {
            DepartureTimes = departureTimes;
            ArrivalTimes = arrivalTimes;
            ToHereTimes = toHereStatement;
            OnError = onError;
        }

        public static SpeedOperator Create(IEnumerable<Statement> stopUntil,IEnumerable<Statement> stopAtUntil,IEnumerable<Statement> stopAt,IEnumerable<Statement> toHereAt,
            IReadOnlyDictionary<string, TrainInfo> trainInfos, Action<string, string, int, int> onError)
        {
            Validator validator = new Validator(onError);

            Dictionary<TrainStopObject, TimeSpan> departureTimes = new Dictionary<TrainStopObject, TimeSpan>();
            Dictionary<TrainStopObject, TimeSpan> arrivalTimes = new Dictionary<TrainStopObject, TimeSpan>();
            Dictionary<TrainStopObject, Statement> toHereTimes = new Dictionary<TrainStopObject, Statement>();
            foreach (Statement statement in stopUntil)
            {
                MapStatement statementSource = statement.Source;

                validator.CheckClauseLength(statementSource, 6);

                TrainInfo trainInfo = validator.GetTrain(statementSource, trainInfos).Info;
                if (!(trainInfo is null))
                {
                    try
                    {
                        double deceleration = Convert.ToDouble(statementSource.Clauses[5].Args[0]) / 3.6;
                        TimeSpan departureTime = CompatibleTimeSpanFactory.Parse(statementSource.Clauses[5].Args[1]);
                        double acceleration = Convert.ToDouble(statementSource.Clauses[5].Args[2]) / 3.6;
                        double speed = Convert.ToDouble(statementSource.Clauses[5].Args[3]) / 3.6;

                        TrainStopObject stop = new TrainStopObject(statementSource.Location, deceleration, 1, acceleration, speed);
                        trainInfo.Insert(stop);
                        departureTimes[stop] = departureTime;
                    }
                    catch (SyntaxException ex)
                    {
                        validator.ThrowError(ex.Message, statementSource);
                    }
                }
            }
            foreach (Statement statement in stopAtUntil)
            {
                MapStatement statementSource = statement.Source;

                validator.CheckClauseLength(statementSource, 6);

                TrainInfo trainInfo = validator.GetTrain(statementSource, trainInfos).Info;
                if (!(trainInfo is null))
                {
                    try
                    {
                        TimeSpan arrivalTime = CompatibleTimeSpanFactory.Parse(statementSource.Clauses[5].Args[0]);
                        TimeSpan departureTime = CompatibleTimeSpanFactory.Parse(statementSource.Clauses[5].Args[1]);
                        double acceleration = Convert.ToDouble(statementSource.Clauses[5].Args[2]) / 3.6;
                        double speed = Convert.ToDouble(statementSource.Clauses[5].Args[3]) / 3.6;

                        TrainStopObject stop = new TrainStopObject(statementSource.Location, 1, 1, acceleration, speed);
                        trainInfo.Insert(stop);
                        departureTimes[stop] = departureTime;
                        arrivalTimes[stop] = arrivalTime;
                    }
                    catch (SyntaxException ex)
                    {
                        validator.ThrowError(ex.Message, statementSource);
                    }
                }
            }
            foreach (Statement statement in stopAt)
            {
                MapStatement statementSource = statement.Source;

                validator.CheckClauseLength(statementSource, 6);

                TrainInfo trainInfo = validator.GetTrain(statementSource, trainInfos).Info;
                if (!(trainInfo is null))
                {
                    try
                    {
                        TimeSpan arrivalTime = CompatibleTimeSpanFactory.Parse(statementSource.Clauses[5].Args[0]);
                        int stopTime = (int)(Convert.ToDouble(statementSource.Clauses[5].Args[1])*1000);
                        double acceleration = Convert.ToDouble(statementSource.Clauses[5].Args[2]) / 3.6;
                        double speed = Convert.ToDouble(statementSource.Clauses[5].Args[3]) / 3.6;

                        TrainStopObject stop = new TrainStopObject(statementSource.Location, 1, stopTime, acceleration, speed);
                        trainInfo.Insert(stop);
                        arrivalTimes[stop] = arrivalTime;
                    }
                    catch (SyntaxException ex)
                    {
                        validator.ThrowError(ex.Message, statementSource);
                    }
                }
            }
            foreach (Statement statement in toHereAt)
            {
                MapStatement statementSource = statement.Source;

                validator.CheckClauseLength(statementSource, 7);

                TrainInfo trainInfo = validator.GetTrain(statementSource, trainInfos).Info;
                if (!(trainInfo is null))
                {
                    try
                    {
                        double speed = Convert.ToDouble(statementSource.Clauses[6].Args[0]) / 3.6;
                        TimeSpan toHereTime = CompatibleTimeSpanFactory.Parse(statementSource.Clauses[6].Args[1]);

                        TrainStopObject stop = new TrainStopObject(statementSource.Location, 0, 0,0, speed);
                        trainInfo.Insert(stop);
                        toHereTimes[stop] = statement;
                    }
                    catch (SyntaxException ex)
                    {
                        validator.ThrowError(ex.Message, statementSource);
                    }
                }
            }
            return new SpeedOperator(departureTimes,arrivalTimes,toHereTimes, onError);
        }
        public IEnumerable<TrainSchedule> CompileToSchedules(TrainInfo trainInfo, TimeSpan originTime)
        {
            if (trainInfo.Count == 0) yield break;

            TimeSpan time = TimeSpan.Zero;
            for (int i = 0; i < trainInfo.Count - 1; i++)
            {
                int j = trainInfo.Direction < 0 ? trainInfo.Count - 1 - i : i;
                TrainStopObject prev = (TrainStopObject)trainInfo[j];
                TrainStopObject next = (TrainStopObject)trainInfo[j + trainInfo.Direction];

                if (ToHereTimes.TryGetValue(next, out Statement statement))
                {
                    if(statement is null) {
                        continueFunc();
                        continue;
                    }

                    Validator validator = new Validator(OnError);

            MapStatement statementSource = statement.Source;

                    int direction = trainInfo.Direction;
                    double initialLocation = statementSource.Location;

                    TrainStopObject firstStop = new TrainStopObject(initialLocation, double.MaxValue, 0, double.MaxValue, 0);
                    int firstIndex = InsertStop(firstStop);

                    int prevIndex = firstIndex - direction;
                    int nextIndex = firstIndex + direction;
                    if (prevIndex < 0 || trainInfo.Count <= prevIndex || nextIndex < 0 || trainInfo.Count <= nextIndex)
                    {
                        validator.ThrowError("このステートメントは 2 つの Train.Stop ステートメントの間に定義する必要があります。", statementSource);
                        continueFunc();
                        continue;
                    }
                    //TrainStopObject prevStop = (TrainStopObject)trainInfo[prevIndex];

                    double initialSpeed = prev.Speed;
                    double targetSpeed = Convert.ToDouble(statementSource.Clauses[6].Args[0]) / 3.6;

                    double acceleration = 20;
                    
                    TimeSpan at=CompatibleTimeSpanFactory.Parse(statementSource.Clauses[6].Args[1]);
                    
                        double SL = initialLocation;
                        double PL = prev.Location;
                        double PS = initialSpeed;
                    TimeSpan timeb =time+ TimeSpan.Zero;
                    if (TimeSpan.Zero < prev.StopTime || i == 0)
                    {
                        timeb += prev.StopTime;

                        if (DepartureTimes.TryGetValue(prev, out TimeSpan departureTime))
                        {
                            if (trainInfo.EnableLocation != 0 || trainInfo.EnableTime != TimeSpan.Zero)
                            {
                                OnError("Train[trainKey].StopUntil ステートメントは Train[trainKey].Enable ステートメントと併用できません。", null, 0, 0);
                            }
                            else
                            {
                                if (timeb < departureTime) timeb = departureTime - originTime;
                            }
                        }
                    }
                    double ac=prev.Acceleration;
                    TimeSpan dt = timeb+originTime;
                    double SP=targetSpeed;
                    double pd=SP-PS;
                    double LS = SL-PL;
                    double tsl = PS*(at.TotalSeconds-dt.TotalSeconds-PS/(2*ac));
                    double dectime=2*(LS-tsl)/(pd);
                    double dc =pd/ dectime;
                    acceleration=dc;/**/

                    firstStop.Speed = initialSpeed;
                    if (targetSpeed == initialSpeed)
                    {
                        continueFunc();
                        continue;
                    }

                    int sign = Math.Sign(targetSpeed - initialSpeed);
                    double signedStepSpeed = SpeedOverrider.StepSpeed * sign;

                    if (Math.Sign(acceleration) != sign){
                        validator.ThrowError($"時刻{dt}に距離程{SL}mを速度{Math.Abs(targetSpeed)*3.6}km/hで通過することは不可能です。", statementSource);
                        continueFunc();
                        continue;
                    }

                    double accelerateDistance = (targetSpeed - initialSpeed) * (targetSpeed + initialSpeed) / 2 / acceleration * direction;
                    
                        initialLocation -= accelerateDistance;
                        firstStop.Location = initialLocation;
                    

                    double lastLocation = initialLocation + accelerateDistance;
                    if (Math.Sign(prev.Location - lastLocation) != Math.Sign(firstStop.Location - lastLocation))
                    {
                        validator.ThrowError("他のステートメントの加減速と干渉します。距離を離してください。", statementSource);
                        continueFunc();
                        continue;
                    }
                    //*
                    for (int k = 1; Math.Sign(initialSpeed + signedStepSpeed * k - targetSpeed) != sign; k++)
                    {
                        double location = initialLocation + signedStepSpeed / acceleration * k * (initialSpeed + signedStepSpeed / 2 * k) * direction;
                        double stopAcceleration = k % 20 == 0 ? double.MaxValue : 0;
                        TrainStopObject stop = new TrainStopObject(location, stopAcceleration, 0, stopAcceleration, initialSpeed + signedStepSpeed * k);
                        trainInfo.Insert(stop);
                    }
                    /**/
                    TrainStopObject lastStop2 = new TrainStopObject(lastLocation, double.MaxValue, 0, double.MaxValue, targetSpeed);
                    trainInfo.Insert(lastStop2);

                    continueFunc();
                    continue;

                    int InsertStop(TrainStopObject stop)
                    {
                        trainInfo.Insert(stop);
                        return trainInfo.IndexOf(stop);
                    }
                    void continueFunc()
                    {
                        trainInfo.Remove(next);
                        i= trainInfo.IndexOf(prev)-1;
                        statement=null;
                    }
                }
                else
                {
                    if (TimeSpan.Zero < prev.StopTime || i == 0)
                    {
                        yield return new TrainSchedule(time, TimeSpan.Zero, prev.Location, 0, 0);
                        time += prev.StopTime;

                        if (DepartureTimes.TryGetValue(prev, out TimeSpan departureTime))
                        {
                            if (trainInfo.EnableLocation != 0 || trainInfo.EnableTime != TimeSpan.Zero)
                            {
                                OnError("Train[trainKey].StopUntil ステートメントは Train[trainKey].Enable ステートメントと併用できません。", null, 0, 0);
                            }
                            else
                            {
                                if (time < departureTime) time = departureTime - originTime;
                            }
                        }
                    }

                    double prevAccelerationInv = 0 < prev.Acceleration ? 1 / prev.Acceleration : 0;
                    double nextDecelerationInv = 0 < next.Deceleration ? 1 / next.Deceleration : 0;

                    if (ArrivalTimes.TryGetValue(next, out TimeSpan at))
                    {
                        TimeSpan dt = time+originTime;
                        double NL = next.Location;
                        double PL = prev.Location;
                        double SP = prev.Speed;
                        double ac = prev.Acceleration;
                        double dc = -SP/((NL-PL)*trainInfo.Direction/SP+SP/2/ac-at.TotalSeconds+dt.TotalSeconds)/2;
                        next.Deceleration=dc;
                        nextDecelerationInv=1/dc;
                        if (dc<0)
                        {
                            OnError($"列車の速度が低すぎるか加減速が干渉します. {dt}, {PL} to {at}, {NL}", null, 0, 0);
                        }
                    }

                    double finishAccelerateLocation = prev.Location + prev.Speed * prev.Speed * prevAccelerationInv / 2 * trainInfo.Direction;
                    double beginDecelerateLocation = next.Location - prev.Speed * prev.Speed * nextDecelerationInv / 2 * trainInfo.Direction;

                    bool needCoasting = finishAccelerateLocation * trainInfo.Direction < beginDecelerateLocation * trainInfo.Direction;
                    if (needCoasting)
                    {
                        if (prevAccelerationInv != 0)
                        {
                            TimeSpan accelerateDuration = CompatibleTimeSpanFactory.FromSeconds(prev.Speed * prevAccelerationInv);
                            yield return new TrainSchedule(time, time, prev.Location, 0, prev.Acceleration * trainInfo.Direction);
                            time += accelerateDuration;
                        }

                        TimeSpan coastDuration = CompatibleTimeSpanFactory.FromSeconds((beginDecelerateLocation - finishAccelerateLocation) / prev.Speed * trainInfo.Direction);
                        yield return new TrainSchedule(time, time, finishAccelerateLocation, prev.Speed * trainInfo.Direction, 0);
                        time += coastDuration;

                        if (nextDecelerationInv != 0)
                        {
                            TimeSpan decelerateDuration = CompatibleTimeSpanFactory.FromSeconds(prev.Speed * nextDecelerationInv);
                            yield return new TrainSchedule(time, time + decelerateDuration, next.Location, 0, -next.Deceleration * trainInfo.Direction);
                            time += decelerateDuration;
                        }
                    }
                    else if(finishAccelerateLocation * trainInfo.Direction > beginDecelerateLocation * trainInfo.Direction&&ArrivalTimes.TryGetValue(next, out at))
                    {
                        TimeSpan dt = time+originTime;
                        double maxSpeed = 2 * trainInfo.Direction * (next.Location - prev.Location) /(at-dt).TotalSeconds;

                        TimeSpan accelerateDuration = CompatibleTimeSpanFactory.FromSeconds(maxSpeed * prevAccelerationInv);
                        yield return new TrainSchedule(time, time, prev.Location, 0, prev.Acceleration * trainInfo.Direction);
                        time += accelerateDuration;

                        TimeSpan decelerateDuration = at-dt-accelerateDuration;
                        next.Deceleration=maxSpeed/decelerateDuration.TotalSeconds;
                        yield return new TrainSchedule(time, time + decelerateDuration, next.Location, 0, -next.Deceleration * trainInfo.Direction);
                        time += decelerateDuration;
                    }
                    else
                    {
                        float maxSpeed = (float)Math.Sqrt(2 * trainInfo.Direction * (next.Location - prev.Location) / (prevAccelerationInv + nextDecelerationInv));

                        TimeSpan accelerateDuration = CompatibleTimeSpanFactory.FromSeconds(maxSpeed * prevAccelerationInv);
                        yield return new TrainSchedule(time, time, prev.Location, 0, prev.Acceleration * trainInfo.Direction);
                        time += accelerateDuration;

                        TimeSpan decelerateDuration = CompatibleTimeSpanFactory.FromSeconds(maxSpeed * nextDecelerationInv);
                        yield return new TrainSchedule(time, time + decelerateDuration, next.Location, 0, -next.Deceleration * trainInfo.Direction);
                        time += decelerateDuration;
                    }
                }
            }

            TrainStopObject lastStop = (TrainStopObject)trainInfo[trainInfo.Direction < 0 ? 0 : trainInfo.Count - 1];
            yield return new TrainSchedule(time, TimeSpan.Zero, lastStop.Location, 0, 0);
        }
    }
}
