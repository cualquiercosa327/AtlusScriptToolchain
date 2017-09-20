﻿using System;

namespace AtlusScriptLib.FlowScriptLanguage.BinaryModel
{
    public enum FlowScriptBinarySectionType : uint
    {
        ProcedureLabelSection,
        JumpLabelSection,
        TextSection,
        MessageScriptSection,
        StringSection,
    }

    [Flags]
    public enum FlowScriptBinaryFormatVersion : uint
    {
        Unknown = 1 << 0,
        Version1 = 1 << 1,
        Version2 = 1 << 2,
        Version3 = 1 << 3,
        BigEndian = 1 << 15,
        Version1BigEndian = Version1 | BigEndian,
        Version2BigEndian = Version2 | BigEndian,
        Version3BigEndian = Version3 | BigEndian,
    }
}