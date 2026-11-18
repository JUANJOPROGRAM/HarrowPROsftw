using System.Runtime.InteropServices;
using HarrowStudio.Application;
using HarrowStudio.Domain;

namespace HarrowStudio.Infrastructure;

public sealed class XInputControllerInput : IControllerInput
{
    private const uint ErrorSuccess = 0;
    private const uint JoyReturnAll = 0x000000FF;
    private const ushort GamepadStart = 0x0010;
    private const ushort GamepadBack = 0x0020;
    private const ushort GamepadLeftThumb = 0x0040;
    private const ushort GamepadRightThumb = 0x0080;
    private const string XInputDll = "xinput1_4.dll";

    [DllImport(XInputDll, EntryPoint = "XInputGetState", ExactSpelling = true)]
    private static extern uint XInputGetState(uint userIndex, out XInputState state);

    [DllImport("winmm.dll", EntryPoint = "joyGetNumDevs", ExactSpelling = true)]
    private static extern uint JoyGetNumDevs();

    [DllImport("winmm.dll", EntryPoint = "joyGetPosEx", ExactSpelling = true)]
    private static extern uint JoyGetPosEx(uint deviceId, ref JoyInfoEx info);

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState { public uint PacketNumber; public Gamepad Gamepad; }
    [StructLayout(LayoutKind.Sequential)]
    private struct Gamepad
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short ThumbLX;
        public short ThumbLY;
        public short ThumbRX;
        public short ThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JoyInfoEx
    {
        public uint Size;
        public uint Flags;
        public uint X;
        public uint Y;
        public uint Z;
        public uint R;
        public uint U;
        public uint V;
        public uint Buttons;
        public uint ButtonNumber;
        public uint Pov;
        public uint Reserved1;
        public uint Reserved2;
    }

    public ControllerState Read()
    {
        try
        {
            for (uint slot = 0; slot < 4; slot++)
            {
                if (XInputGetState(slot, out var state) != ErrorSuccess) continue;
                var g = state.Gamepad;
                var buttons = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                Add(0x1000, "A"); Add(0x2000, "B"); Add(0x4000, "X"); Add(0x8000, "Y");
                Add(0x0100, "LB"); Add(0x0200, "RB");
                Add(GamepadBack, "Back"); Add(GamepadStart, "Start");
                Add(GamepadLeftThumb, "L3"); Add(GamepadRightThumb, "R3");
                Add(0x0001, "D-pad ↑"); Add(0x0002, "D-pad ↓");
                Add(0x0004, "D-pad ←"); Add(0x0008, "D-pad →");
                if (g.LeftTrigger > 24) buttons.Add("LT");
                if (g.RightTrigger > 24) buttons.Add("RT");
                return new ControllerState
                {
                    IsConnected = true, InputMode = "XInput", PressedButtons = buttons,
                    LeftX = Normalize(g.ThumbLX), LeftY = Normalize(g.ThumbLY),
                    RightX = Normalize(g.ThumbRX), RightY = Normalize(g.ThumbRY),
                    LeftTrigger = g.LeftTrigger / 255d, RightTrigger = g.RightTrigger / 255d
                };

                void Add(ushort mask, string name) { if ((g.Buttons & mask) != 0) buttons.Add(name); }
            }
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }

        return ReadDirectInput();
    }

    private static ControllerState ReadDirectInput()
    {
        try
        {
            var deviceCount = Math.Min(JoyGetNumDevs(), 16u);
            for (uint deviceId = 0; deviceId < deviceCount; deviceId++)
            {
                var info = new JoyInfoEx
                {
                    Size = (uint)Marshal.SizeOf<JoyInfoEx>(),
                    Flags = JoyReturnAll
                };
                if (JoyGetPosEx(deviceId, ref info) != ErrorSuccess) continue;

                var buttons = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                Add(0, "A"); Add(1, "B"); Add(2, "X"); Add(3, "Y");
                Add(4, "LB"); Add(5, "RB"); Add(6, "Back"); Add(7, "Start");
                Add(8, "L3"); Add(9, "R3"); Add(10, "LT"); Add(11, "RT");
                AddPov(info.Pov, buttons);
                return new ControllerState
                {
                    IsConnected = true,
                    InputMode = "DInput",
                    PressedButtons = buttons,
                    LeftX = NormalizeAxis(info.X),
                    LeftY = -NormalizeAxis(info.Y),
                    RightX = NormalizeAxis(info.R),
                    RightY = -NormalizeAxis(info.U)
                };

                void Add(int index, string name)
                {
                    if ((info.Buttons & (1u << index)) != 0) buttons.Add(name);
                }
            }
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }

        return new ControllerState();
    }

    private static void AddPov(uint pov, HashSet<string> buttons)
    {
        if (pov == uint.MaxValue) return;
        var angle = (int)((pov + 2250) % 36000 / 4500) * 45;
        if (angle is 315 or 0 or 45) buttons.Add("D-pad ↑");
        if (angle is 45 or 90 or 135) buttons.Add("D-pad →");
        if (angle is 135 or 180 or 225) buttons.Add("D-pad ↓");
        if (angle is 225 or 270 or 315) buttons.Add("D-pad ←");
    }

    private static double Normalize(short value) => Math.Clamp(value / 32767d, -1d, 1d);
    private static double NormalizeAxis(uint value) => Math.Clamp((value - 32767.5d) / 32767.5d, -1d, 1d);
}
