﻿using Battlehub.RTSaveLoad;
using UnityEngine;
using UnityEngine.EventSystems;

using Battlehub.RTCommon;

namespace Battlehub.RTHandles
{
    public class RotationHandle : BaseHandle
    {
        public float GridSize = 15.0f;
        public float XSpeed = 1.0f;
        public float YSpeed = 1.0f;

        private const float innerRadius = 1.0f;
        private const float outerRadius = 1.2f;
        private const float hitDot = 0.2f;

        private float m_deltaX;
        private float m_deltaY;
        private Vector2 m_prevPointer;

        public override RuntimeTool Tool
        {
            get { return RuntimeTool.Rotate; }
        }

        private Quaternion m_targetRotation = Quaternion.identity;
        private Quaternion m_startingRotation = Quaternion.identity;
        private Quaternion StartingRotation
        {
            get { return Editor.Tools.PivotRotation == RuntimePivotRotation.Global ? m_startingRotation : Quaternion.identity; }
        }

        private Quaternion m_startinRotationInv = Quaternion.identity;
        private Quaternion StartingRotationInv
        {
            get { return Editor.Tools.PivotRotation == RuntimePivotRotation.Global ? m_startinRotationInv : Quaternion.identity; }
        }

        private Quaternion m_targetInverse = Quaternion.identity;
        private Matrix4x4 m_targetInverseMatrix;
        private Vector3 m_startingRotationAxis = Vector3.zero;

        protected override float CurrentGridUnitSize
        {
            get { return GridSize; }
        }

        protected override void AwakeOverride()
        {
            base.AwakeOverride();
            Editor.Tools.PivotRotationChanged += OnPivotRotationChanged;
        }

        protected override void OnDestroyOverride()
        {
            base.OnDestroyOverride();    
            Editor.Tools.PivotRotationChanged -= OnPivotRotationChanged;
        }

        protected override void OnStartOverride()
        {
            base.OnStartOverride();    
            OnPivotRotationChanged();
        }

        protected override void OnEnableOverride()
        {
            base.OnEnableOverride();
            OnPivotRotationChanged();
        }

        protected override void UpdateOverride()
        {
            base.UpdateOverride();
            if (Editor.Tools.IsViewing)
            {
                SelectedAxis = RuntimeHandleAxis.None;
                return;
            }
            if (!IsWindowActive || !Window.IsPointerOver)
            {
                return;
            }
            if (!IsDragging && !IsPointerDown)
            {
                if (HightlightOnHover)
                {
                    m_targetInverseMatrix = Matrix4x4.TRS(Target.position, Target.rotation * StartingRotationInv, Vector3.one).inverse;
                    SelectedAxis = Hit();
                }

                if (m_targetRotation != Target.rotation)
                {
                    m_startingRotation = Target.rotation;
                    m_startinRotationInv = Quaternion.Inverse(m_startingRotation);
                    m_targetRotation = Target.rotation;
                }
            }
        }

        private void OnPivotRotationChanged()
        {
            if (Target != null)
            {
                m_startingRotation = Target.rotation;
                m_startinRotationInv = Quaternion.Inverse(Target.rotation);
                m_targetRotation = Target.rotation;
            }
        }

        private bool Intersect(Ray r, Vector3 sphereCenter, float sphereRadius, out float hit1Distance, out float hit2Distance)
        {
            hit1Distance = 0.0f;
            hit2Distance = 0.0f;

            Vector3 L = sphereCenter - r.origin;
            float tc = Vector3.Dot(L, r.direction);
            if (tc < 0.0)
            {
                return false;
            }

            float d2 = Vector3.Dot(L, L) - (tc * tc);
            float radius2 = sphereRadius * sphereRadius;
            if (d2 > radius2)
            {
                return false;
            }

            float t1c = Mathf.Sqrt(radius2 - d2);
            hit1Distance = tc - t1c;
            hit2Distance = tc + t1c;

            return true;
        }

        private RuntimeHandleAxis Hit()
        {
            if (Model != null)
            {
                return Model.HitTest(Window.Pointer);
            }

            float hit1Distance;
            float hit2Distance;
            Ray ray = Window.Pointer;
            float scale = RuntimeHandlesComponent.GetScreenScale(Target.position, Window.Camera) * Appearance.HandleScale;
            if (Intersect(ray, Target.position, outerRadius * scale, out hit1Distance, out hit2Distance))
            {
                Vector3 dpHitPoint;
                GetPointOnDragPlane(GetDragPlane(), ray, out dpHitPoint);

                RuntimeHandleAxis axis = HitAxis();
                if (axis != RuntimeHandleAxis.None)
                {
                    return axis;
                }

                bool isInside = (dpHitPoint - Target.position).magnitude <= innerRadius * scale;

                if (isInside)
                {
                    return RuntimeHandleAxis.Free;
                }
                else
                {
                    return RuntimeHandleAxis.Screen;
                }
            }

            return RuntimeHandleAxis.None;
        }

        private RuntimeHandleAxis HitAxis()
        {
            float screenScale = RuntimeHandlesComponent.GetScreenScale(Target.position, Window.Camera) * Appearance.HandleScale;
            Vector3 scale = new Vector3(screenScale, screenScale, screenScale);
            Matrix4x4 xTranform = Matrix4x4.TRS(Vector3.zero, Target.rotation * StartingRotationInv * Quaternion.AngleAxis(-90, Vector3.up), Vector3.one);
            Matrix4x4 yTranform = Matrix4x4.TRS(Vector3.zero, Target.rotation * StartingRotationInv * Quaternion.AngleAxis(-90, Vector3.right), Vector3.one);
            Matrix4x4 zTranform = Matrix4x4.TRS(Vector3.zero, Target.rotation * StartingRotationInv, Vector3.one);
            Matrix4x4 objToWorld = Matrix4x4.TRS(Target.position, Quaternion.identity, scale);

            float xDistance;
            float yDistance;
            float zDistance;
            bool hitX = HitAxis(xTranform, objToWorld, out xDistance);
            bool hitY = HitAxis(yTranform, objToWorld, out yDistance);
            bool hitZ = HitAxis(zTranform, objToWorld, out zDistance);

            if (hitX && xDistance < yDistance && xDistance < zDistance)
            {
                return RuntimeHandleAxis.X;
            }
            else if (hitY && yDistance < xDistance && yDistance < zDistance)
            {
                return RuntimeHandleAxis.Y;
            }
            else if (hitZ && zDistance < xDistance && zDistance < yDistance)
            {
                return RuntimeHandleAxis.Z;
            }

            return RuntimeHandleAxis.None;
        }

        private bool HitAxis(Matrix4x4 transform, Matrix4x4 objToWorld, out float minDistance)
        {
            bool hit = false;
            minDistance = float.PositiveInfinity;

            const float radius = 1.0f;
            const int pointsPerCircle = 32;
            float angle = 0.0f;
            float z = 0.0f;

            Vector3 zeroCamPoint = transform.MultiplyPoint(Vector3.zero);
            zeroCamPoint = objToWorld.MultiplyPoint(zeroCamPoint);
            zeroCamPoint = Window.Camera.worldToCameraMatrix.MultiplyPoint(zeroCamPoint);

            Vector3 prevPoint = transform.MultiplyPoint(new Vector3(radius, 0, z));
            prevPoint = objToWorld.MultiplyPoint(prevPoint);
            for (int i = 0; i < pointsPerCircle; i++)
            {
                angle += 2 * Mathf.PI / pointsPerCircle;
                float x = radius * Mathf.Cos(angle);
                float y = radius * Mathf.Sin(angle);
                Vector3 point = transform.MultiplyPoint(new Vector3(x, y, z));
                point = objToWorld.MultiplyPoint(point);

                Vector3 camPoint = Window.Camera.worldToCameraMatrix.MultiplyPoint(point);

                if (camPoint.z > zeroCamPoint.z)
                {
                    Vector3 screenVector = Window.Camera.WorldToScreenPoint(point) - Window.Camera.WorldToScreenPoint(prevPoint);
                    float screenVectorMag = screenVector.magnitude;
                    screenVector.Normalize();
                    if (screenVector != Vector3.zero)
                    {
                        float distance;
                        if (HitScreenAxis(out distance, Window.Camera.WorldToScreenPoint(prevPoint), screenVector, screenVectorMag))
                        {
                            if (distance < minDistance)
                            {
                                minDistance = distance;
                                hit = true;
                            }
                        }
                    }
                }

                prevPoint = point;
            }
            return hit;
        }

        protected override bool OnBeginDrag()
        {
            if(!base.OnBeginDrag())
            {
                return false;
            }

            m_targetRotation = Target.rotation;
            m_targetInverseMatrix = Matrix4x4.TRS(Target.position, Target.rotation * StartingRotationInv, Vector3.one).inverse;
            SelectedAxis = Hit();
            m_deltaX = 0.0f;
            m_deltaY = 0.0f;

            Vector2 point;
            if (Window.Pointer.XY(Target.position, out point))
            {  
                m_prevPointer = point;
            }
            else
            {
                SelectedAxis = RuntimeHandleAxis.None;
            }

            if (SelectedAxis == RuntimeHandleAxis.Screen)
            {
                Vector2 center;

                if (Window.Pointer.WorldToScreenPoint(Target.position, Target.position, out center))
                {
                    if (Window.Pointer.XY(Target.position, out point))
                    {
                        float angle = Mathf.Atan2(point.y - center.y, point.x - center.x);
                        m_targetInverse = Quaternion.Inverse(Quaternion.AngleAxis(Mathf.Rad2Deg * angle, Vector3.forward));
                        m_targetInverseMatrix = Matrix4x4.TRS(Target.position, Target.rotation, Vector3.one).inverse;
                        m_prevPointer = point;
                    }
                    else
                    {
                        SelectedAxis = RuntimeHandleAxis.None;
                    }
                }
                else
                {
                    SelectedAxis = RuntimeHandleAxis.None;
                }     
            }
            else
            {
                if (SelectedAxis == RuntimeHandleAxis.X)
                {
                    m_startingRotationAxis = (Target.rotation * Quaternion.Inverse(StartingRotation)) * Vector3.right;
                }
                else if (SelectedAxis == RuntimeHandleAxis.Y)
                {
                    m_startingRotationAxis = (Target.rotation * Quaternion.Inverse(StartingRotation)) * Vector3.up;
                }
                else if (SelectedAxis == RuntimeHandleAxis.Z)
                {
                    m_startingRotationAxis = (Target.rotation * Quaternion.Inverse(StartingRotation)) * Vector3.forward;
                }

                m_targetInverse = Quaternion.Inverse(Target.rotation);
            }

            return SelectedAxis != RuntimeHandleAxis.None;
        }

        protected override void OnDrag()
        {
            base.OnDrag();

            Vector2 point;
            if (!Window.Pointer.XY(Target.position, out point))
            {
                return;
            }

            float deltaX = point.x - m_prevPointer.x;
            float deltaY = point.y - m_prevPointer.y;
            m_prevPointer = point;

            deltaX = deltaX * XSpeed;
            deltaY = deltaY * YSpeed;

            m_deltaX += deltaX;
            m_deltaY += deltaY;

            Matrix4x4 toWorldMatrix;
            if (!Window.Pointer.ToWorldMatrix(Target.position, out toWorldMatrix))
            {
                return;
            }

            Vector3 delta = StartingRotation * Quaternion.Inverse(Target.rotation) * toWorldMatrix.MultiplyVector(new Vector3(m_deltaY, -m_deltaX, 0));
            Quaternion rotation = Quaternion.identity;
            if (SelectedAxis == RuntimeHandleAxis.X)
            {
                Vector3 rotationAxis = Quaternion.Inverse(Target.rotation) * m_startingRotationAxis;

                if (EffectiveGridUnitSize != 0.0f)
                {
                    if (Mathf.Abs(delta.x) >= EffectiveGridUnitSize)
                    {
                        delta.x = Mathf.Sign(delta.x) * EffectiveGridUnitSize;
                        m_deltaX = 0.0f;
                        m_deltaY = 0.0f;
                    }
                    else
                    {
                        delta.x = 0.0f;
                    }
                }

                if(LockObject.RotationX)
                {
                    delta.x = 0.0f;
                }

                rotation = Quaternion.AngleAxis(delta.x, rotationAxis);
            }
            else if (SelectedAxis == RuntimeHandleAxis.Y)
            {
                Vector3 rotationAxis = Quaternion.Inverse(Target.rotation) * m_startingRotationAxis;

                if (EffectiveGridUnitSize != 0.0f)
                {
                    if (Mathf.Abs(delta.y) >= EffectiveGridUnitSize)
                    {
                        delta.y = Mathf.Sign(delta.y) * EffectiveGridUnitSize;
                        m_deltaX = 0.0f;
                        m_deltaY = 0.0f;
                    }
                    else
                    {
                        delta.y = 0.0f;
                    }
                }

                if (LockObject.RotationY)
                {
                    delta.y = 0.0f;
                }

                rotation = Quaternion.AngleAxis(delta.y, rotationAxis);

            }
            else if (SelectedAxis == RuntimeHandleAxis.Z)
            {
                Vector3 rotationAxis = Quaternion.Inverse(Target.rotation) * m_startingRotationAxis;

                if (EffectiveGridUnitSize != 0.0f)
                {
                    if (Mathf.Abs(delta.z) >= EffectiveGridUnitSize)
                    {
                        delta.z = Mathf.Sign(delta.z) * EffectiveGridUnitSize;
                        m_deltaX = 0.0f;
                        m_deltaY = 0.0f;
                    }
                    else
                    {
                        delta.z = 0.0f;
                    }
                }

                if (LockObject.RotationZ)
                {
                    delta.z = 0.0f;
                }

                rotation = Quaternion.AngleAxis(delta.z, rotationAxis);

            }
            else if (SelectedAxis == RuntimeHandleAxis.Free)
            {
                delta = StartingRotationInv * delta;

                if (LockObject.RotationX)
                {
                    delta.x = 0.0f;
                }

                if (LockObject.RotationY)
                {
                    delta.y = 0.0f;
                }

                if (LockObject.RotationZ)
                {
                    delta.z = 0.0f;
                }

                rotation = Quaternion.Euler(delta.x, delta.y, delta.z);
                m_deltaX = 0.0f;
                m_deltaY = 0.0f;
            }
            else
            {
                delta = m_targetInverse * new Vector3(m_deltaY, -m_deltaX, 0);
                if (EffectiveGridUnitSize != 0.0f)
                {
                    if (Mathf.Abs(delta.x) >= EffectiveGridUnitSize)
                    {
                        delta.x = Mathf.Sign(delta.x) * EffectiveGridUnitSize;
                        m_deltaX = 0.0f;
                        m_deltaY = 0.0f;
                    }
                    else
                    {
                        delta.x = 0.0f;
                    }
                }


                Vector3 axis = m_targetInverseMatrix.MultiplyVector(Window.Camera.cameraToWorldMatrix.MultiplyVector(-Vector3.forward));

                if(!LockObject.RotationScreen)
                {
                    rotation = Quaternion.AngleAxis(delta.x, axis);
                }
            }

            if (EffectiveGridUnitSize == 0.0f)
            {
                m_deltaX = 0.0f;
                m_deltaY = 0.0f;
            }


            for (int i = 0; i < ActiveTargets.Length; ++i)
            {
                ActiveTargets[i].rotation *= rotation;
            }
        }

        protected override void OnDrop()
        {
            base.OnDrop();
            m_targetRotation = Target.rotation;
        }

        protected override void SyncModelTransform()
        {
            base.SyncModelTransform();
            Model.transform.rotation = Target.rotation * StartingRotationInv;
        }

        protected override void DrawOverride()
        {
            Appearance.DoRotationHandle(Target.rotation * StartingRotationInv, Target.position, SelectedAxis, LockObject, Editor.IsVR);
        }
    }
}
