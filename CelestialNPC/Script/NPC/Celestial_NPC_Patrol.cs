using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace CelestialCyclesSystem
{
    public class Celestial_NPC_Patrol : Celestial_NPC
    {
        private SplineContainer areaSpline;
        private Spline currentPath;
        private float startXOffset;
        public bool needsToResumePath = false;
        public bool isReversed;
        public float m_SplineLength;
        public Vector3 resumePositionLocation;

        [Range(0, 1)] public float currentPositionSpline;
        [Range(0, 1)] public float resumePositionSpline;

        protected override void Awake()
        {
            base.Awake();
            if (celestialAreas.areaType == Celestial_Areas.AreaType.Path)
            {
                areaSpline = celestialAreas.currentSpline;
                currentPath = areaSpline.Spline;
                m_SplineLength = currentPath.GetLength();
                currentPositionSpline = Random.Range(0f, 1f);
                isReversed = Random.value > 0.5f;
                startXOffset = Random.Range(0.1f, celestialAreas.pathWidth);
            }
        }

        protected override NPCState GetInitialState()
        {
            return NPCState.FollowingPath;
        }

        protected override void HandleFollowingPath()
        {
            if (!stamina.InNeed()) // Updated to use stamina.InNeed()
            {
                if (needsToResumePath)
                {
                    ResumePathFollowing();
                }
                else
                {
                    UpdatePositionAlongSpline();
                }
            }
        }

        private void ResumePathFollowing()
        {
            float distanceToResumePoint = Vector3.Distance(transform.position, resumePositionLocation);
            _navComponent.SetDestination(resumePositionLocation);
            if (distanceToResumePoint <= 1.0f)
            {
                currentPositionSpline = resumePositionSpline;
                needsToResumePath = false;
                ChangeState(NPCState.FollowingPath);
            }
        }

        public void PausePathFollowing()
        {
            resumePositionSpline = currentPositionSpline;
            resumePositionLocation = GetWorldPositionFromSpline(resumePositionSpline);
            needsToResumePath = true;
            _navComponent.enabled = true;
        }

        private void UpdatePositionAlongSpline()
        {
            if (celestialAreas == null || areaSpline == null) return;
            _navComponent.enabled = false;
            UpdateAnimationWalkSpeed(movementSpeed);
            MoveOnSpline();
        }

        private Vector3 GetWorldPositionFromSpline(float normalizedPosition)
        {
            Vector3 localPosition = currentPath.EvaluatePosition(normalizedPosition);
            return transform.TransformPoint(localPosition);
        }

        private void MoveOnSpline()
        {
            float splineSpeed = movementSpeed / m_SplineLength * Time.deltaTime;
            currentPositionSpline += isReversed ? -splineSpeed : splineSpeed;

            if (currentPositionSpline > 1f)
            {
                currentPositionSpline = 1f;
                isReversed = true;
            }
            else if (currentPositionSpline < 0f)
            {
                currentPositionSpline = 0f;
                isReversed = false;
            }

            currentPath.Evaluate(Unity.Mathematics.math.frac(currentPositionSpline), out var pos, out var tangent, out var up);
            Vector3 worldPos = pos;
            Vector3 offsetPos = isReversed ? new Vector3(startXOffset, 0, 0) : new Vector3(-startXOffset, 0, 0);
            transform.position = worldPos + offsetPos;

            Vector3 forwardDirection = isReversed ? -tangent : tangent;
            Quaternion targetRotation = Quaternion.LookRotation(forwardDirection, up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 15f);
        }
    }
}