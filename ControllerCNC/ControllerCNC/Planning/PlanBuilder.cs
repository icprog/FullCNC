﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows;

using ControllerCNC.Machine;
using ControllerCNC.Primitives;
using System.Diagnostics;

namespace ControllerCNC.Planning
{
    public class PlanBuilder
    {
        /// <summary>
        /// Plan which is built.
        /// </summary>
        private readonly List<InstructionCNC> _plan = new List<InstructionCNC>();

        /// <summary>
        /// Builds the plan.
        /// </summary>
        /// <returns>The plan built.</returns>
        public IEnumerable<InstructionCNC> Build()
        {
            return _plan.ToArray();
        }

        /// <summary>
        /// Changes instructions for XY to UV.
        /// </summary>
        public void ChangeXYtoUV()
        {
            var planCopy = _plan.ToArray();
            _plan.Clear();
            foreach (Axes instruction in planCopy)
            {
                _plan.Add(instruction.AsUV());
            }
        }

        /// <summary>
        /// Duplicates instructions for XY to UV.
        /// </summary>
        public void DuplicateXYtoUV()
        {
            var planCopy = _plan.ToArray();
            _plan.Clear();
            foreach (Axes instruction in planCopy)
            {
                _plan.Add(instruction.DuplicateToUV());
            }
        }

        /// <summary>
        /// Adds a plan instruction for simultaneous controll of two axes.
        /// The instruction type has to be same for the axes.
        /// </summary>
        /// <param name="instructionX">instruction for the x axis.</param>
        /// <param name="instructionY">instruction for the y axis.</param>
        public void AddXY(StepInstrution instructionX, StepInstrution instructionY)
        {
            _plan.Add(Axes.XY(instructionX, instructionY));
        }

        /// <summary>
        /// Adds a plan instruction for simultaneous controll of all axes.
        /// The instruction type has to be same for the axes.
        /// </summary>
        /// <param name="instructionU">instruction for the u axis.</param>
        /// <param name="instructionV">instruction for the v axis.</param>
        /// <param name="instructionX">instruction for the x axis.</param>
        /// <param name="instructionY">instruction for the y axis.</param>
        public void AddUVXY(StepInstrution instructionU, StepInstrution instructionV, StepInstrution instructionX, StepInstrution instructionY)
        {
            _plan.Add(Axes.UVXY(instructionU, instructionV, instructionX, instructionY));
        }

        /// <summary>
        /// Add given instructions.
        /// </summary>
        public void Add(IEnumerable<InstructionCNC> plan)
        {
            _plan.AddRange(plan);
        }

        /// <summary>
        /// Adds acceleration for x and y axes.
        /// </summary>
        /// <param name="accelerationProfileX">Profile for x axis acceleration.</param>
        /// <param name="accelerationProfileY">Profile for y axis acceleration.</param>
        public void AddAccelerationXY(AccelerationBuilder accelerationProfileX, AccelerationBuilder accelerationProfileY)
        {
            AddXY(accelerationProfileX.ToInstruction(), accelerationProfileY.ToInstruction());
        }

        /// <summary>
        /// Adds acceleration for u, v, x and y axes.
        /// </summary>
        public void AddAccelerationUVXY(AccelerationBuilder accelerationProfileU, AccelerationBuilder accelerationProfileV, AccelerationBuilder accelerationProfileX, AccelerationBuilder accelerationProfileY)
        {
            AddUVXY(accelerationProfileU.ToInstruction(), accelerationProfileV.ToInstruction(), accelerationProfileX.ToInstruction(), accelerationProfileY.ToInstruction());
        }


        /// <summary>
        /// Adds transition with specified entry, cruise and leaving speeds by RPM.
        /// </summary>
        /// <param name="stepCount">How many steps will be done.</param>
        /// <param name="startRPM">RPM at the start.</param>
        /// <param name="targetRPM">Cruise RPM.</param>
        /// <param name="endRPM">RPM at the end.</param>
        public void AddTransitionRPM(int stepCount, int startRPM, int targetRPM, int endRPM)
        {
            var startDeltaT = DeltaTFromRPM(startRPM);
            var targetDeltaT = DeltaTFromRPM(targetRPM);
            var endDeltaT = DeltaTFromRPM(endRPM);

            //SEND_Transition(stepCount, startDeltaT, targetDeltaT, endDeltaT);
            throw new NotImplementedException("Refactoring");
        }


        /// <summary>
        /// Adds 2D transition with constant speed.
        /// </summary>
        /// <param name="distanceX">Distance along X axis in steps.</param>
        /// <param name="distanceY">Distance along Y axis in steps.</param>
        /// <param name="transitionSpeed">Speed of the transition.</param>
        public void AddConstantSpeedTransitionXY(int distanceX, int distanceY, Speed transitionSpeed)
        {
            checked
            {
                var sqrt = Math.Sqrt(1.0 * distanceX * distanceX + 1.0 * distanceY * distanceY);
                var transitionTime = (long)(sqrt * transitionSpeed.Ticks / transitionSpeed.StepCount);

                var remainingStepsX = distanceX;
                var remainingStepsY = distanceY;

                var chunkLengthLimit = 31500;
                var chunkCount = 1.0 * Math.Max(Math.Abs(distanceX), Math.Abs(distanceY)) / chunkLengthLimit;
                chunkCount = Math.Max(1, chunkCount);


                var doneDistanceX = 0L;
                var doneDistanceY = 0L;
                var doneTime = 0.0;

                var i = Math.Min(1.0, chunkCount);
                while (Math.Abs(remainingStepsX) > 0 || Math.Abs(remainingStepsY) > 0)
                {
                    var chunkDistanceX = distanceX * i / chunkCount;
                    var chunkDistanceY = distanceY * i / chunkCount;
                    var chunkTime = transitionTime * i / chunkCount;

                    var stepCountXD = chunkDistanceX - doneDistanceX;
                    var stepCountYD = chunkDistanceY - doneDistanceY;
                    var stepsTime = chunkTime - doneTime;

                    var stepCountX = (Int16)Math.Round(stepCountXD);
                    var stepCountY = (Int16)Math.Round(stepCountYD);

                    doneDistanceX += stepCountX;
                    doneDistanceY += stepCountY;

                    //we DON'T want to round here - this way we can distribute time precisely
                    var stepTimeX = stepCountX == 0 ? 0 : (int)(stepsTime / Math.Abs(stepCountX));
                    var stepTimeY = stepCountY == 0 ? 0 : (int)(stepsTime / Math.Abs(stepCountY));

                    var timeRemainderX = Math.Abs(stepCountXD) <= 1 ? (UInt16)0 : (UInt16)(stepsTime % Math.Abs(stepCountXD));
                    var timeRemainderY = Math.Abs(stepCountYD) <= 1 ? (UInt16)0 : (UInt16)(stepsTime % Math.Abs(stepCountYD));

                    if (stepTimeX == 0 && stepTimeY == 0)
                    {
                        if (doneDistanceX == distanceX && doneDistanceY == distanceY)
                            break;
                        throw new NotImplementedException("Send wait signal");
                    }

                    doneTime += stepsTime;

                    var xPart = createConstant(stepCountX, stepTimeX, timeRemainderX);
                    var yPart = createConstant(stepCountY, stepTimeY, timeRemainderY);
                    AddXY(xPart, yPart);
                    i = i + 1 > chunkCount ? chunkCount : i + 1;
                }
            }
        }

        /// <summary>
        /// Adds 4D transition with constant speed.
        /// </summary>
        /// <param name="distanceU">Distance along U axis in steps.</param>
        /// <param name="distanceV">Distance along V axis in steps.</param>
        /// <param name="transitionSpeedUV">Speed of the transition in uv plane.</param>
        /// <param name="distanceX">Distance along X axis in steps.</param>
        /// <param name="distanceY">Distance along Y axis in steps.</param>
        /// <param name="transitionSpeedXY">Speed of the transition in xy plane.</param>
        public void AddConstantSpeedTransitionUVXY(int distanceU, int distanceV, Speed transitionSpeedUV, int distanceX, int distanceY, Speed transitionSpeedXY)
        {
            checked
            {
                var sqrtUV = Math.Sqrt(1.0 * distanceU * distanceU + 1.0 * distanceV * distanceV);
                var sqrtXY = Math.Sqrt(1.0 * distanceX * distanceX + 1.0 * distanceY * distanceY);
                var transitionTimeUV = transitionSpeedUV.StepCount == 0 ? 0 : (long)(sqrtUV * transitionSpeedUV.Ticks / transitionSpeedUV.StepCount);
                var transitionTimeXY = transitionSpeedXY.StepCount == 0 ? 0 : (long)(sqrtXY * transitionSpeedXY.Ticks / transitionSpeedXY.StepCount);
                var transitionTime = Math.Max(transitionTimeUV, transitionTimeXY);

                var remainingStepsU = distanceU;
                var remainingStepsV = distanceV;
                var remainingStepsX = distanceX;
                var remainingStepsY = distanceY;

                var chunkLengthLimit = Configuration.MaxStepInstructionLimit;
                var maxUV = Math.Max(Math.Abs(distanceU), Math.Abs(distanceV));
                var maxXY = Math.Max(Math.Abs(distanceX), Math.Abs(distanceY));
                var chunkCount = (int)Math.Ceiling(1.0 * Math.Max(maxUV, maxXY) / chunkLengthLimit);

                var doneDistanceU = 0L;
                var doneDistanceV = 0L;
                var doneDistanceX = 0L;
                var doneDistanceY = 0L;
                var doneTime = 0.0;

                for (var doneChunks = 1; doneChunks <= chunkCount; ++doneChunks)
                {
                    var chunkDistanceU = 1.0 * distanceU * doneChunks / chunkCount;
                    var chunkDistanceV = 1.0 * distanceV * doneChunks / chunkCount;
                    var chunkDistanceX = 1.0 * distanceX * doneChunks / chunkCount;
                    var chunkDistanceY = 1.0 * distanceY * doneChunks / chunkCount;
                    var chunkTime = 1.0 * transitionTime * doneChunks / chunkCount;

                    var stepCountUD = chunkDistanceU - doneDistanceU;
                    var stepCountVD = chunkDistanceV - doneDistanceV;
                    var stepCountXD = chunkDistanceX - doneDistanceX;
                    var stepCountYD = chunkDistanceY - doneDistanceY;
                    var stepsTime = chunkTime - doneTime;

                    var stepCountU = (Int16)Math.Round(stepCountUD);
                    var stepCountV = (Int16)Math.Round(stepCountVD);
                    var stepCountX = (Int16)Math.Round(stepCountXD);
                    var stepCountY = (Int16)Math.Round(stepCountYD);

                    doneDistanceU += stepCountU;
                    doneDistanceV += stepCountV;
                    doneDistanceX += stepCountX;
                    doneDistanceY += stepCountY;

                    //we DON'T want to round here - this way we can distribute time precisely via time remainders
                    var stepTimeU = stepCountU == 0 ? 0 : (int)(stepsTime / Math.Abs(stepCountU));
                    var stepTimeV = stepCountV == 0 ? 0 : (int)(stepsTime / Math.Abs(stepCountV));
                    var stepTimeX = stepCountX == 0 ? 0 : (int)(stepsTime / Math.Abs(stepCountX));
                    var stepTimeY = stepCountY == 0 ? 0 : (int)(stepsTime / Math.Abs(stepCountY));

                    var timeRemainderU = Math.Abs(stepCountUD) <= 1 ? (UInt16)0 : (UInt16)(stepsTime % Math.Abs(stepCountUD));
                    var timeRemainderV = Math.Abs(stepCountVD) <= 1 ? (UInt16)0 : (UInt16)(stepsTime % Math.Abs(stepCountVD));
                    var timeRemainderX = Math.Abs(stepCountXD) <= 1 ? (UInt16)0 : (UInt16)(stepsTime % Math.Abs(stepCountXD));
                    var timeRemainderY = Math.Abs(stepCountYD) <= 1 ? (UInt16)0 : (UInt16)(stepsTime % Math.Abs(stepCountYD));

                    if (stepTimeU == 0 && stepTimeV == 0 && stepTimeX == 0 && stepTimeY == 0)
                    {
                        if (doneDistanceU == distanceU && doneDistanceV == distanceV && doneDistanceX == distanceX && doneDistanceY == distanceY)
                            break;
                        throw new NotImplementedException("Send wait signal");
                    }

                    doneTime += stepsTime;

                    var uPart = createConstant(stepCountU, stepTimeU, timeRemainderU);
                    var vPart = createConstant(stepCountV, stepTimeV, timeRemainderV);
                    var xPart = createConstant(stepCountX, stepTimeX, timeRemainderX);
                    var yPart = createConstant(stepCountY, stepTimeY, timeRemainderY);
                    AddUVXY(uPart, vPart, xPart, yPart);
                }

                if (
                    distanceU != doneDistanceU ||
                    distanceV != doneDistanceV ||
                    distanceX != doneDistanceX ||
                    distanceY != doneDistanceY
                    )
                    throw new NotImplementedException("Distance was not calculated precisely. Needs to be fixed.");
            }
        }

        /// <summary>
        /// Adds ramped line with specified number of steps.
        /// </summary>
        /// <param name="xSteps">Number of steps along x.</param>
        /// <param name="ySteps">Numer of steps along y.</param>
        /// <param name="planeAcceleration">Acceleration used for ramping - calculated for both axis combined.</param>
        /// <param name="planeSpeedLimit">Maximal speed that could be achieved for both axis combined.</param>
        public void AddRampedLineXY(int xSteps, int ySteps, Acceleration planeAcceleration, Speed planeSpeedLimit)
        {
            if (xSteps == 0 && ySteps == 0)
                //nothing to do
                return;
            Speed speedLimitX, speedLimitY;
            DecomposeXY(xSteps, ySteps, planeSpeedLimit, out speedLimitX, out speedLimitY);

            Acceleration accelerationX, accelerationY;
            DecomposeXY(xSteps, ySteps, planeAcceleration, out accelerationX, out accelerationY);

            Speed reachedX, reachedY;
            int accelerationStepsX, accelerationStepsY;
            var timeX = AccelerationBuilder.CalculateTime(Speed.Zero, speedLimitX, accelerationX, xSteps / 2, out reachedX, out accelerationStepsX);
            var timeY = AccelerationBuilder.CalculateTime(Speed.Zero, speedLimitY, accelerationY, ySteps / 2, out reachedY, out accelerationStepsY);

            //take acceleration time according to axis with more precision
            var accelerationTime = Math.Max(timeX, timeY);

            var accelerationProfileX = AccelerationBuilder.FromTo(Speed.Zero, reachedX, accelerationStepsX, accelerationTime);
            var accelerationProfileY = AccelerationBuilder.FromTo(Speed.Zero, reachedY, accelerationStepsY, accelerationTime);

            var reachedSpeedX = Speed.FromDeltaT(accelerationProfileX.EndDelta + accelerationProfileX.BaseDeltaT);
            var reachedSpeedY = Speed.FromDeltaT(accelerationProfileY.EndDelta + accelerationProfileY.BaseDeltaT);
            var reachedSpeed = ComposeSpeeds(reachedSpeedX, reachedSpeedY);

            var decelerationProfileX = AccelerationBuilder.FromTo(reachedX, Speed.Zero, accelerationStepsX, accelerationTime);
            var decelerationProfileY = AccelerationBuilder.FromTo(reachedY, Speed.Zero, accelerationStepsY, accelerationTime);

            var remainingX = xSteps - accelerationProfileX.StepCount - decelerationProfileX.StepCount;
            var remainingY = ySteps - accelerationProfileY.StepCount - decelerationProfileY.StepCount;

            //send ramp
            AddAccelerationXY(accelerationProfileX, accelerationProfileY);
            AddConstantSpeedTransitionXY(remainingX, remainingY, reachedSpeed);
            AddAccelerationXY(decelerationProfileX, decelerationProfileY);
        }

        /// <summary>
        /// Adds ramped line with specified number of steps.
        /// </summary>
        /// <param name="uSteps">Number of steps along u.</param>
        /// <param name="vSteps">Numer of steps along v.</param>
        /// <param name="xSteps">Number of steps along x.</param>
        /// <param name="ySteps">Numer of steps along y.</param>
        /// <param name="planeAcceleration">Acceleration used for ramping - calculated for both axis combined.</param>
        /// <param name="planeSpeedLimit">Maximal speed that could be achieved for both axis combined.</param>
        public void AddRampedLineUVXY(int uSteps, int vSteps, int xSteps, int ySteps, Acceleration planeAcceleration, Speed planeSpeedLimit)
        {
            if (uSteps == 0 && vSteps == 0 && xSteps == 0 && ySteps == 0)
                //nothing to do
                return;

            Speed speedLimitU, speedLimitV;
            DecomposeXY(uSteps, vSteps, planeSpeedLimit, out speedLimitU, out speedLimitV);

            Acceleration accelerationU, accelerationV;
            DecomposeXY(uSteps, vSteps, planeAcceleration, out accelerationU, out accelerationV);

            Speed speedLimitX, speedLimitY;
            DecomposeXY(xSteps, ySteps, planeSpeedLimit, out speedLimitX, out speedLimitY);

            Acceleration accelerationX, accelerationY;
            DecomposeXY(xSteps, ySteps, planeAcceleration, out accelerationX, out accelerationY);

            Speed reachedU, reachedV, reachedX, reachedY;
            int accelerationStepsU, accelerationStepsV, accelerationStepsX, accelerationStepsY;
            var timeU = AccelerationBuilder.CalculateTime(Speed.Zero, speedLimitU, accelerationU, uSteps / 2, out reachedU, out accelerationStepsU);
            var timeV = AccelerationBuilder.CalculateTime(Speed.Zero, speedLimitV, accelerationV, vSteps / 2, out reachedV, out accelerationStepsV);
            var timeX = AccelerationBuilder.CalculateTime(Speed.Zero, speedLimitX, accelerationX, xSteps / 2, out reachedX, out accelerationStepsX);
            var timeY = AccelerationBuilder.CalculateTime(Speed.Zero, speedLimitY, accelerationY, ySteps / 2, out reachedY, out accelerationStepsY);

            //take acceleration time according to axis with more precision
            var accelerationTime = Math.Max(Math.Max(timeU, timeV), Math.Max(timeX, timeY));

            var accelerationProfileU = AccelerationBuilder.FromTo(Speed.Zero, reachedU, accelerationStepsU, accelerationTime);
            var accelerationProfileV = AccelerationBuilder.FromTo(Speed.Zero, reachedV, accelerationStepsV, accelerationTime);
            var accelerationProfileX = AccelerationBuilder.FromTo(Speed.Zero, reachedX, accelerationStepsX, accelerationTime);
            var accelerationProfileY = AccelerationBuilder.FromTo(Speed.Zero, reachedY, accelerationStepsY, accelerationTime);

            var reachedSpeedU = Speed.FromDeltaT(accelerationProfileU.EndDelta + accelerationProfileU.BaseDeltaT);
            var reachedSpeedV = Speed.FromDeltaT(accelerationProfileV.EndDelta + accelerationProfileV.BaseDeltaT);
            var reachedSpeedX = Speed.FromDeltaT(accelerationProfileX.EndDelta + accelerationProfileX.BaseDeltaT);
            var reachedSpeedY = Speed.FromDeltaT(accelerationProfileY.EndDelta + accelerationProfileY.BaseDeltaT);

            var reachedSpeedUV = ComposeSpeeds(reachedSpeedU, reachedSpeedV);
            var reachedSpeedXY = ComposeSpeeds(reachedSpeedX, reachedSpeedY);

            var decelerationProfileU = AccelerationBuilder.FromTo(reachedU, Speed.Zero, accelerationStepsU, accelerationTime);
            var decelerationProfileV = AccelerationBuilder.FromTo(reachedV, Speed.Zero, accelerationStepsV, accelerationTime);
            var decelerationProfileX = AccelerationBuilder.FromTo(reachedX, Speed.Zero, accelerationStepsX, accelerationTime);
            var decelerationProfileY = AccelerationBuilder.FromTo(reachedY, Speed.Zero, accelerationStepsY, accelerationTime);

            var remainingU = uSteps - accelerationProfileU.StepCount - decelerationProfileU.StepCount;
            var remainingV = vSteps - accelerationProfileV.StepCount - decelerationProfileV.StepCount;
            var remainingX = xSteps - accelerationProfileX.StepCount - decelerationProfileX.StepCount;
            var remainingY = ySteps - accelerationProfileY.StepCount - decelerationProfileY.StepCount;

            //send ramp
            AddAccelerationUVXY(accelerationProfileU, accelerationProfileV, accelerationProfileX, accelerationProfileY);
            AddConstantSpeedTransitionUVXY(remainingU, remainingV, reachedSpeedUV, remainingX, remainingY, reachedSpeedXY);
            AddAccelerationUVXY(decelerationProfileU, decelerationProfileV, decelerationProfileX, decelerationProfileY);
        }


        /// <summary>
        /// Adds line with specified initial and desired speed.
        /// </summary>
        /// <param name="xSteps">Number of steps along x.</param>
        /// <param name="ySteps">Numer of steps along y.</param>
        /// <param name="planeAcceleration">Acceleration used for getting desired speed out of initial.</param>
        public Speed AddLineXY(int xSteps, int ySteps, Speed initialSpeed, Acceleration planeAcceleration, Speed desiredEndSpeed)
        {
            if (xSteps == 0 && ySteps == 0)
                //nothing to do
                return initialSpeed;

            Speed speedLimitX, speedLimitY;
            DecomposeXY(xSteps, ySteps, desiredEndSpeed, out speedLimitX, out speedLimitY);

            Speed initialSpeedX, initialSpeedY;
            DecomposeXY(xSteps, ySteps, initialSpeed, out initialSpeedX, out initialSpeedY);

            Acceleration accelerationX, accelerationY;
            DecomposeXY(xSteps, ySteps, planeAcceleration, out accelerationX, out accelerationY);

            Speed reachedX, reachedY;
            int accelerationStepsX, accelerationStepsY;
            var timeX = AccelerationBuilder.CalculateTime(initialSpeedX, speedLimitX, accelerationX, xSteps, out reachedX, out accelerationStepsX);
            var timeY = AccelerationBuilder.CalculateTime(initialSpeedY, speedLimitY, accelerationY, ySteps, out reachedY, out accelerationStepsY);

            //take acceleration time according to axis with more precision
            var accelerationTime = Math.Max(timeX, timeY);

            var accelerationProfileX = AccelerationBuilder.FromTo(initialSpeedX, reachedX, accelerationStepsX, accelerationTime);
            var accelerationProfileY = AccelerationBuilder.FromTo(initialSpeedY, reachedY, accelerationStepsY, accelerationTime);

            var reachedSpeedX = Speed.FromDeltaT(accelerationProfileX.EndDelta + accelerationProfileX.BaseDeltaT);
            var reachedSpeedY = Speed.FromDeltaT(accelerationProfileY.EndDelta + accelerationProfileY.BaseDeltaT);
            var reachedSpeed = ComposeSpeeds(reachedSpeedX, reachedSpeedY);

            var remainingX = xSteps - accelerationProfileX.StepCount;
            var remainingY = ySteps - accelerationProfileY.StepCount;

            if (accelerationProfileX.StepCount == 0 && accelerationProfileY.StepCount == 0)
                reachedSpeed = initialSpeed;

            //send profile
            AddAccelerationXY(accelerationProfileX, accelerationProfileY);
            AddConstantSpeedTransitionXY(remainingX, remainingY, reachedSpeed);

            return reachedSpeed;
        }

        #region Acceleration calculation utilities

        /// <summary>
        /// Compose separate axes speeds into a plane speed.
        /// </summary>
        /// <param name="speedX">Speed for x axis.</param>
        /// <param name="speedY">Speed for y axis.</param>
        /// <returns>The composed speed.</returns>
        public Speed ComposeSpeeds(Speed speedX, Speed speedY)
        {
            checked
            {
                var composedSpeed = Math.Sqrt(1.0 * speedX.StepCount * speedX.StepCount / speedX.Ticks / speedX.Ticks + 1.0 * speedY.StepCount * speedY.StepCount / speedY.Ticks / speedY.Ticks);

                var resolution = Configuration.TimerFrequency * 1000;
                return new Speed((long)Math.Round(Math.Abs(composedSpeed * resolution)), resolution);
            }
        }

        /// <summary>
        /// Decomposes plane speed into separate axes speeds in a direction specified by step counts.
        /// </summary>
        /// <param name="planeSpeed">Speed within the plane</param>
        /// <param name="speedX">Output speed for x axis.</param>
        /// <param name="speedY">Output speed for y axis.</param>
        public void DecomposeXY(int stepsX, int stepsY, Speed planeSpeed, out Speed speedX, out Speed speedY)
        {
            //TODO verify/improve precision
            checked
            {
                if (stepsX == 0 && stepsY == 0)
                {
                    speedX = Speed.Zero;
                    speedY = Speed.Zero;
                    return;
                }

                var direction = new Vector(stepsX, stepsY);
                direction.Normalize();

                var speedVector = direction * planeSpeed.StepCount / planeSpeed.Ticks;
                var resolution = Configuration.TimerFrequency;

                speedX = new Speed((long)Math.Round(Math.Abs(speedVector.X * resolution)), resolution);
                speedY = new Speed((long)Math.Round(Math.Abs(speedVector.Y * resolution)), resolution);
            }
        }

        /// <summary>
        /// Decomposes plane acceleration into separate axes accelerations in a direction specified by step counts.
        /// </summary>
        public void DecomposeXY(int stepsX, int stepsY, Acceleration planeAcceleration, out Acceleration accelerationX, out Acceleration accelerationY)
        {
            checked
            {
                Speed speedX, speedY;
                DecomposeXY(stepsX, stepsY, planeAcceleration.Speed, out speedX, out speedY);

                accelerationX = new Acceleration(speedX, planeAcceleration.Ticks);
                accelerationY = new Acceleration(speedY, planeAcceleration.Ticks);
            }
        }

        #endregion

        #region Planning related calculation utilities

        public Int16 GetStepSlice(long steps, Int16 maxSize = 30000)
        {
            maxSize = Math.Abs(maxSize);
            if (steps > 0)
                return (Int16)Math.Min(maxSize, steps);
            else
                return (Int16)Math.Max(-maxSize, steps);
        }


        public int DeltaTFromRPM(int rpm)
        {
            if (rpm == 0)
                return Configuration.StartDeltaT;

            checked
            {
                var deltaT = Configuration.TimerFrequency * 60 / 400 / rpm;
                return (int)deltaT;
            }
        }
        #endregion

        #region Obsolete acceleration calculation
        [Obsolete("Use correct acceleration profiles instead")]
        internal static AccelerationInstruction CalculateBoundedAcceleration(int startDeltaT, int endDeltaT, Int16 accelerationDistanceLimit, int accelerationNumerator = 1, int accelerationDenominator = 1)
        {
            checked
            {
                if (accelerationDistanceLimit == 0)
                {
                    return new AccelerationInstruction(0, 0, 0, 0, 0);
                }

                var stepSign = accelerationDistanceLimit >= 0 ? 1 : -1;
                var limit = Math.Abs(accelerationDistanceLimit);

                var startN = calculateN(startDeltaT, accelerationNumerator, accelerationDenominator);
                var endN = calculateN(endDeltaT, accelerationNumerator, accelerationDenominator);

                Int16 stepCount;
                if (startN < endN)
                {
                    //acceleration
                    stepCount = (Int16)(Math.Min(endN - startN, limit));
                    var limitedDeltaT = calculateDeltaT(startDeltaT, startN, stepCount, accelerationNumerator, accelerationDenominator);
                    return new AccelerationInstruction((Int16)(stepCount * stepSign), startDeltaT, 0, 0, startN);
                }
                else
                {
                    //deceleration
                    stepCount = (Int16)(Math.Min(startN - endN, limit));
                    var limitedDeltaT = calculateDeltaT(startDeltaT, (Int16)(-startN), stepCount, accelerationNumerator, accelerationDenominator);
                    return new AccelerationInstruction((Int16)(stepCount * stepSign), startDeltaT, 0, 0, (Int16)(-startN));
                }
            }
        }

        private static UInt16 calculateDeltaT(int startDeltaT, Int16 startN, Int16 stepCount, int accelerationNumerator, int accelerationDenominator)
        {
            checked
            {
                var endN = Math.Abs(startN + stepCount);
                return (UInt16)(Configuration.TimerFrequency / Math.Sqrt(2.0 * endN * (long)Configuration.MaxAcceleration));
            }
        }

        private static Int16 calculateN(int startDeltaT, int accelerationNumerator, int accelerationDenominator)
        {
            checked
            {
                var n1 = (long)Configuration.TimerFrequency * Configuration.TimerFrequency * accelerationDenominator / 2 / startDeltaT / startDeltaT / Configuration.MaxAcceleration / accelerationNumerator;
                return (Int16)Math.Max(1, n1);
            }
        }

        #endregion

        #region Private utilities

        /// <summary>
        /// Creates a plan part for constant speed segment.
        /// </summary>      
        private ConstantInstruction createConstant(Int16 stepCount, int baseTime, UInt16 timeRemainder)
        {
            checked
            {
                return new ConstantInstruction(stepCount, baseTime, timeRemainder);
            }
        }

        #endregion
    }
}
