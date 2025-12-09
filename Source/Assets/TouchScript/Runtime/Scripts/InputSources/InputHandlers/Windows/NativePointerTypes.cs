#if UNITY_STANDALONE_WIN
using System;
using System.Runtime.InteropServices;

namespace TouchScript.InputSources.InputHandlers.Interop
{
    enum TOUCH_API
    {
        WIN7,
        WIN8
    }

    enum PointerEvent : uint
    {
        Enter = 0x0249,
        Leave = 0x024A,
        Update = 0x0245,
        Down = 0x0246,
        Up = 0x0247,
        Cancelled = 0x1000
    }

    enum PointerType
    {
        Pointer = 0x00000001,
        Touch = 0x00000002,
        Pen = 0x00000003,
        Mouse = 0x00000004,
        TouchPad = 0x00000005
    }

    [Flags]
    enum PointerFlags
    {
        None = 0x00000000,
        New = 0x00000001,
        InRange = 0x00000002,
        InContact = 0x00000004,
        FirstButton = 0x00000010,
        SecondButton = 0x00000020,
        ThirdButton = 0x00000040,
        FourthButton = 0x00000080,
        FifthButton = 0x00000100,
        Primary = 0x00002000,
        Confidence = 0x00004000,
        Canceled = 0x00008000,
        Down = 0x00010000,
        Update = 0x00020000,
        Up = 0x00040000,
        Wheel = 0x00080000,
        HWheel = 0x00100000,
        CaptureChanged = 0x00200000,
        HasTransform = 0x00400000
    }

    enum ButtonChangeType
    {
        None,
        FirstDown,
        FirstUp,
        SecondDown,
        SecondUp,
        ThirdDown,
        ThirdUp,
        FourthDown,
        FourthUp,
        FifthDown,
        FifthUp
    }

    [Flags]
    enum TouchFlags
    {
        None = 0x00000000
    }

    [Flags]
    enum TouchMask
    {
        None = 0x00000000,
        ContactArea = 0x00000001,
        Orientation = 0x00000002,
        Pressure = 0x00000004
    }

    [Flags]
    enum PenFlags
    {
        None = 0x00000000,
        Barrel = 0x00000001,
        Inverted = 0x00000002,
        Eraser = 0x00000004
    }

    [Flags]
    enum PenMask
    {
        None = 0x00000000,
        Pressure = 0x00000001,
        Rotation = 0x00000002,
        TiltX = 0x00000004,
        TiltY = 0x00000008
    }

    [StructLayout(LayoutKind.Sequential)]
    struct PointerData
    {
        public PointerFlags PointerFlags;
        public uint Flags;
        public uint Mask;
        public ButtonChangeType ChangedButtons;
        public uint Rotation;
        public uint Pressure;
        public int TiltX;
        public int TiltY;
    }

    enum FeedbackType : uint
    {
        FeedbackTouchContactVisualization = 1,
        FeedbackPenBarrelVisualization = 2,
        FeedbackPenTap = 3,
        FeedbackDoubleTap = 4,
        FeedbackPenPressAndHold = 5,
        FeedbackPenRightTap = 6,
        FeedbackTouchTap = 7,
        FeedbackTouchDoubleTap = 8,
        FeedbackTouchPressAndHold = 9,
        FeedbackTouchRightTap = 10,
        FeedbackGesturePressAndTap = 11,
        FeedbackMax = 0xFFFFFFFF
    }
}
#endif