﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

using MyoSharp.Discovery;
using MyoSharp.Internal;
using MyoSharp.Math;

namespace MyoSharp.Device
{
    public class Myo : IMyo
    {
        #region Fields
        private readonly IHub _hub;
        private IntPtr _handle;
        private bool _disposed;
        #endregion

        #region Constructors
        protected Myo(IHub hub, IntPtr handle)
        {
            _hub = hub;
            _handle = handle;

            _hub.RouteMyoEvent += Hub_RouteMyoEvent;
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="Myo"/> class.
        /// </summary>
        ~Myo()
        {
            Dispose(false);
        }
        #endregion

        #region Properties
        public IntPtr Handle
        {
            get { return _handle; }
        }
        
        protected IHub Hub
        {
            get { return _hub; }
        }

        #endregion

        #region Events
        public event EventHandler<MyoEventArgs> Connected;

        public event EventHandler<MyoEventArgs> Disconnected;

        public event EventHandler<ArmRecognizedEventArgs> ArmRecognized;

        public event EventHandler<MyoEventArgs> ArmLost;

        public event EventHandler<PoseEventArgs> PoseChanged;

        public event EventHandler<OrientationDataEventArgs> OrientationDataAcquired;

        public event EventHandler<AccelerometerDataEventArgs> AccelerometerDataAcquired;

        public event EventHandler<GyroscopeDataEventArgs> GyroscopeDataAcquired;

        public event EventHandler<RssiEventArgs> Rssi;
        #endregion

        #region Methods
        /// <summary>
        /// Creates a new <see cref="IMyo"/> instance.
        /// </summary>
        /// <param name="hub">The hub this Myo should listen to.</param>
        /// <param name="handle">The handle of the Myo device.</param>
        /// <returns>A new <see cref="IMyo"/> instance.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the hub is null.</exception>
        /// <exception cref="System.ArgumentException">Thrown when the handle is not set.</exception>
        public static IMyo Create(IHub hub, IntPtr handle)
        {
            if (hub == null)
            {
                throw new ArgumentNullException("hub", "The hub cannot be null.");
            }

            if (handle == null)
            {
                throw new ArgumentException("The handle must be set.", "handle");
            }

            return new Myo(hub, handle);
        }

        /// <summary>
        /// Causes the Myo to vibrate.
        /// </summary>
        /// <param name="type">The type of vibration.</param>
        public void Vibrate(VibrationType type)
        {
            if (PlatformInvocation.Running32Bit)
            {
                vibrate_32(_handle, type, IntPtr.Zero);
            }
            else
            {
                vibrate_64(_handle, type, IntPtr.Zero);
            }
        }

        /// <summary>
        /// Requests RSSI from the Myo.
        /// </summary>
        public void RequestRssi()
        {
            if (PlatformInvocation.Running32Bit)
            {
                request_rssi_32(_handle, IntPtr.Zero);
            }
            else
            {
                request_rssi_64(_handle, IntPtr.Zero);
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Handles an event that was received for this device.
        /// </summary>
        /// <param name="type">The type of the event.</param>
        /// <param name="timestamp">The timestamp of the event.</param>
        /// <param name="evt">The pointer to the event.</param>
        protected virtual void HandleEvent(MyoEventType type, DateTime timestamp, IntPtr evt)
        {
            switch (type)
            {
                case MyoEventType.Connected:
                    OnConnected(timestamp);
                    break;

                case MyoEventType.Disconnected:
                    OnDisconnected(timestamp);
                    break;

                case MyoEventType.ArmRecognized:
                    OnArmRecognized(evt, timestamp);
                    break;

                case MyoEventType.ArmLost:
                    OnArmLost(timestamp);
                    break;

                case MyoEventType.Orientation:
                    OnOrientationChanged(evt, timestamp);
                    break;

                case MyoEventType.Pose:
                    OnPoseChanged(evt, timestamp);
                    break;

                case MyoEventType.Rssi:
                    OnRssi(evt, timestamp);
                    break;
            }
        }

        /// <summary>
        /// Called when the Myo sends a <see cref="MyoEventType.Rssi"/> event.
        /// </summary>
        /// <param name="evt">The pointer to the event.</param>
        /// <param name="timestamp">The timestamp of the event.</param>
        protected virtual void OnRssi(IntPtr evt, DateTime timestamp)
        {
            var handler = Rssi;
            if (handler != null)
            {
                var rssi = GetEventRssi(evt);
                var args = new RssiEventArgs(
                    this, 
                    timestamp, 
                    rssi);
                handler.Invoke(this, args);
            }
        }

        /// <summary>
        /// Called when the Myo has detected a change in the user's pose.
        /// </summary>
        /// <param name="evt">The pointer to the event.</param>
        /// <param name="timestamp">The timestamp of the event.</param>
        protected virtual void OnPoseChanged(IntPtr evt, DateTime timestamp)
        {
            var handler = PoseChanged;
            if (handler != null)
            {
                var pose = GetEventPose(evt);
                var args = new PoseEventArgs(this, timestamp, pose);
                handler.Invoke(this, args);
            }
        }

        /// <summary>
        /// Called when the Myo has detected an orientation change.
        /// </summary>
        /// <param name="evt">The pointer to the event.</param>
        /// <param name="timestamp">The timestamp of the event.</param>
        protected virtual void OnOrientationChanged(IntPtr evt, DateTime timestamp)
        {
            OnAcquiredOrientationData(evt, timestamp);
            OnAcquiredAccelerometerData(evt, timestamp);
            OnAcquiredGyroscopeData(evt, timestamp);
        }

        /// <summary>
        /// Called when gyroscope data has been acquired from the Myo.
        /// </summary>
        /// <param name="evt">The pointer to the event.</param>
        /// <param name="timestamp">The timestamp of the event.</param>
        protected virtual void OnAcquiredGyroscopeData(IntPtr evt, DateTime timestamp)
        {
            var handler = GyroscopeDataAcquired;
            if (handler != null)
            {
                var vector = GetGyroscope(evt);
                var args = new GyroscopeDataEventArgs(
                    this,
                    timestamp,
                    vector);
                GyroscopeDataAcquired(this, args);
            }
        }

        /// <summary>
        /// Called when accelerometer data has been acquired from the Myo.
        /// </summary>
        /// <param name="evt">The pointer to the event.</param>
        /// <param name="timestamp">The timestamp of the event.</param>
        protected virtual void OnAcquiredAccelerometerData(IntPtr evt, DateTime timestamp)
        {
            var handler = AccelerometerDataAcquired;
            if (handler != null)
            {
                var vector = GetEventAccelerometer(evt);
                var args = new AccelerometerDataEventArgs(
                    this, 
                    timestamp, 
                    vector);
                handler.Invoke(this, args);
            }
        }

        /// <summary>
        /// Called when orientation data has been acquired from the Myo.
        /// </summary>
        /// <param name="evt">The pointer to the event.</param>
        /// <param name="timestamp">The timestamp of the event.</param>
        protected virtual void OnAcquiredOrientationData(IntPtr evt, DateTime timestamp)
        {
            var handler = OrientationDataAcquired;
            if (handler != null)
            {
                var vector = GetEventOrientation(evt);
                var args = new OrientationDataEventArgs(
                    this,
                    timestamp,
                    vector);
                handler.Invoke(this, args);
            }
        }

        /// <summary>
        /// Called when the Myo can no longer recognize which arm it is on.
        /// </summary>
        /// <param name="timestamp">The timestamp of the event.</param>
        protected virtual void OnArmLost(DateTime timestamp)
        {
            var handler = ArmLost;
            if (handler != null)
            {
                var args = new MyoEventArgs(this, timestamp);
                handler.Invoke(this, args);
            }
        }

        /// <summary>
        /// Called when the Myo has recognized which arm it is on.
        /// </summary>
        /// <param name="evt">The pointer to the event.</param>
        /// <param name="timestamp">The timestamp of the event.</param>
        protected virtual void OnArmRecognized(IntPtr evt, DateTime timestamp)
        {
            var handler = ArmRecognized;
            if (handler != null)
            {
                var arm = GetArm(evt);
                var xDirection = GetEventDirectionX(evt);
                var args = new ArmRecognizedEventArgs(
                    this,
                    timestamp,
                    arm,
                    xDirection);
                handler.Invoke(this, args);
            }
        }

        /// <summary>
        /// Called when the Myo has disconnected.
        /// </summary>
        /// <param name="timestamp">The timestamp of the event.</param>
        protected virtual void OnDisconnected(DateTime timestamp)
        {
            var handler = Disconnected;
            if (handler != null)
            {
                var args = new MyoEventArgs(this, timestamp);
                handler(this, args);
            }
        }

        /// <summary>
        /// Called when the Myo has connected..
        /// </summary>
        /// <param name="timestamp">The timestamp of the event.</param>
        protected virtual void OnConnected(DateTime timestamp)
        {
            var handler = Connected;
            if (handler != null)
            {
                var args = new MyoEventArgs(this, timestamp);
                handler(this, args);
            }
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                // free managed objects
                if (disposing)
                {
                    _hub.RouteMyoEvent -= Hub_RouteMyoEvent;
                }
            }
            finally
            {
                _disposed = true;
            }
        }

        protected static sbyte GetEventRssi(IntPtr evt)
        {
            return PlatformInvocation.Running32Bit
                ? GetEventRssi(evt)
                : GetEventRssi(evt);
        }

        protected static Vector3F GetEventAccelerometer(IntPtr evt)
        {
            float x = GetEventAccelerometer(evt, 0);
            float y = GetEventAccelerometer(evt, 1);
            float z = GetEventAccelerometer(evt, 2);
            return new Vector3F(x, y, z);
        }

        protected static float GetEventAccelerometer(IntPtr evt, uint index)
        {
            return PlatformInvocation.Running32Bit
                ? event_get_accelerometer_32(evt, index)
                : event_get_accelerometer_64(evt, index);
        }

        protected static XDirection GetEventDirectionX(IntPtr evt)
        {
            return PlatformInvocation.Running32Bit
                ? event_get_x_direction_32(evt)
                : event_get_x_direction_64(evt);
        }

        protected static QuaternionF GetEventOrientation(IntPtr evt)
        {
            float x = GetEventOrientation(evt, OrientationIndex.X);
            float y = GetEventOrientation(evt, OrientationIndex.Y);
            float z = GetEventOrientation(evt, OrientationIndex.Z);
            float w = GetEventOrientation(evt, OrientationIndex.W);
            return new QuaternionF(x, y, z, w);
        }

        protected static float GetEventOrientation(IntPtr evt, OrientationIndex index)
        {
            return PlatformInvocation.Running32Bit
                ? event_get_orientation_32(evt, index)
                : event_get_orientation_64(evt, index);
        }

        protected static float GetFirmwareVersion(IntPtr evt, VersionComponent component)
        {
            return PlatformInvocation.Running32Bit
                ? event_get_firmware_version_32(evt, component)
                : event_get_firmware_version_64(evt, component);
        }

        protected static Arm GetArm(IntPtr evt)
        {
            return PlatformInvocation.Running32Bit
                ? event_get_arm_32(evt)
                : event_get_arm_64(evt);
        }

        protected static Vector3F GetGyroscope(IntPtr evt)
        {
            float x = GetGyroscope(evt, 0);
            float y = GetGyroscope(evt, 1);
            float z = GetGyroscope(evt, 2);
            return new Vector3F(x, y, z);
        }

        protected static float GetGyroscope(IntPtr evt, uint index)
        {
            return PlatformInvocation.Running32Bit
                ? event_get_gyroscope_32(evt, index)
                : event_get_gyroscope_64(evt, index);
        }

        protected static Pose GetEventPose(IntPtr evt)
        {
            return PlatformInvocation.Running32Bit
                ? event_get_pose_32(evt)
                : event_get_pose_64(evt);
        }
        #endregion

        #region PInvokes
        [DllImport(PlatformInvocation.MyoDllPath32, EntryPoint = "libmyo_vibrate", CallingConvention = CallingConvention.Cdecl)]
        private static extern void vibrate_32(IntPtr myo, VibrationType type, IntPtr error);

        [DllImport(PlatformInvocation.MyoDllPath64, EntryPoint = "libmyo_vibrate", CallingConvention = CallingConvention.Cdecl)]
        private static extern void vibrate_64(IntPtr myo, VibrationType type, IntPtr error);

        [DllImport(PlatformInvocation.MyoDllPath32, EntryPoint = "libmyo_request_rssi", CallingConvention = CallingConvention.Cdecl)]
        private static extern void request_rssi_32(IntPtr myo, IntPtr error);

        [DllImport(PlatformInvocation.MyoDllPath64, EntryPoint = "libmyo_request_rssi", CallingConvention = CallingConvention.Cdecl)]
        private static extern void request_rssi_64(IntPtr myo, IntPtr error);

        [DllImport(PlatformInvocation.MyoDllPath32, EntryPoint = "libmyo_event_get_firmware_version", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint event_get_firmware_version_32(IntPtr evt, VersionComponent component);

        [DllImport(PlatformInvocation.MyoDllPath64, EntryPoint = "libmyo_event_get_firmware_version", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint event_get_firmware_version_64(IntPtr evt, VersionComponent component);

        [DllImport(PlatformInvocation.MyoDllPath32, EntryPoint = "libmyo_event_get_arm", CallingConvention = CallingConvention.Cdecl)]
        private static extern Arm event_get_arm_32(IntPtr evt);

        [DllImport(PlatformInvocation.MyoDllPath64, EntryPoint = "libmyo_event_get_arm", CallingConvention = CallingConvention.Cdecl)]
        private static extern Arm event_get_arm_64(IntPtr evt);

        [DllImport(PlatformInvocation.MyoDllPath32, EntryPoint = "libmyo_event_get_x_direction", CallingConvention = CallingConvention.Cdecl)]
        private static extern XDirection event_get_x_direction_32(IntPtr evt);

        [DllImport(PlatformInvocation.MyoDllPath64, EntryPoint = "libmyo_event_get_x_direction", CallingConvention = CallingConvention.Cdecl)]
        private static extern XDirection event_get_x_direction_64(IntPtr evt);

        [DllImport(PlatformInvocation.MyoDllPath32, EntryPoint = "libmyo_event_get_orientation", CallingConvention = CallingConvention.Cdecl)]
        private static extern float event_get_orientation_32(IntPtr evt, OrientationIndex index);

        [DllImport(PlatformInvocation.MyoDllPath64, EntryPoint = "libmyo_event_get_orientation", CallingConvention = CallingConvention.Cdecl)]
        private static extern float event_get_orientation_64(IntPtr evt, OrientationIndex index);

        [DllImport(PlatformInvocation.MyoDllPath32, EntryPoint = "libmyo_event_get_accelerometer", CallingConvention = CallingConvention.Cdecl)]
        private static extern float event_get_accelerometer_32(IntPtr evt, uint index);

        [DllImport(PlatformInvocation.MyoDllPath64, EntryPoint = "libmyo_event_get_accelerometer", CallingConvention = CallingConvention.Cdecl)]
        private static extern float event_get_accelerometer_64(IntPtr evt, uint index);

        [DllImport(PlatformInvocation.MyoDllPath32, EntryPoint = "libmyo_event_get_gyroscope", CallingConvention = CallingConvention.Cdecl)]
        private static extern float event_get_gyroscope_32(IntPtr evt, uint index);

        [DllImport(PlatformInvocation.MyoDllPath64, EntryPoint = "libmyo_event_get_gyroscope", CallingConvention = CallingConvention.Cdecl)]
        private static extern float event_get_gyroscope_64(IntPtr evt, uint index);

        [DllImport(PlatformInvocation.MyoDllPath32, EntryPoint = "libmyo_event_get_pose", CallingConvention = CallingConvention.Cdecl)]
        private static extern Pose event_get_pose_32(IntPtr evt);

        [DllImport(PlatformInvocation.MyoDllPath64, EntryPoint = "libmyo_event_get_pose", CallingConvention = CallingConvention.Cdecl)]
        private static extern Pose event_get_pose_64(IntPtr evt);

        [DllImport(PlatformInvocation.MyoDllPath32, EntryPoint = "libmyo_event_get_rssi", CallingConvention = CallingConvention.Cdecl)]
        private static extern sbyte event_get_rssi_32(IntPtr evt);

        [DllImport(PlatformInvocation.MyoDllPath64, EntryPoint = "libmyo_event_get_rssi", CallingConvention = CallingConvention.Cdecl)]
        private static extern sbyte event_get_rssi_64(IntPtr evt);
        #endregion  

        #region Event handlers
        private void Hub_RouteMyoEvent(object sender, RouteMyoEventArgs e)
        {
            if (e.MyoHandle != _handle)
            {
                return;
            }

            HandleEvent(e.EventType, e.Timestamp, e.Event);
        }
        #endregion
    }
}
