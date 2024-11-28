﻿using AtlusScriptLibrary.Common.Text;
using AtlusScriptLibrary.FlowScriptLanguage.BinaryModel;
using AtlusScriptLibrary.MessageScriptLanguage;
using AtlusScriptLibrary.MessageScriptLanguage.Decompiler;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace AtlusScriptLibrary.FlowScriptLanguage.Disassembler;

public class FlowScriptBinaryDisassembler : IDisposable
{
    private bool mDisposed;
    private readonly string mHeaderString = "This file was generated by AtlusScriptLib";
    private FlowScriptBinary mScript;
    private readonly TextWriter mWriter;
    private int mInstructionIndex;

    private BinaryInstruction CurrentInstruction
    {
        get
        {
            if (mScript == null || mScript.TextSection == null || mScript.TextSection.Count == 0)
                throw new InvalidOperationException("Invalid state");

            return mScript.TextSection[mInstructionIndex];
        }
    }

    private BinaryInstruction? NextInstruction
    {
        get
        {
            if (mScript == null || mScript.TextSection == null || mScript.TextSection.Count == 0)
                return null;

            if ((mInstructionIndex + 1) < (mScript.TextSection.Count - 1))
                return mScript.TextSection[mInstructionIndex + 1];
            return null;
        }
    }

    public FlowScriptBinaryDisassembler(TextWriter writer)
    {
        mWriter = writer;
    }

    public FlowScriptBinaryDisassembler(string outpath)
    {
        mWriter = new FileTextWriter(outpath);
    }

    public FlowScriptBinaryDisassembler(Stream stream)
    {
        mWriter = new StreamWriter(stream);
    }

    public void Disassemble(FlowScriptBinary script)
    {
        mScript = script ?? throw new ArgumentNullException(nameof(script));
        mInstructionIndex = 0;

        WriteDisassembly();
    }

    private void WriteCommentLine(string text)
    {
        mWriter.WriteLine("# " + text);
    }

    private void WriteDisassembly()
    {
        WriteHeader();

        if (mScript.TextSection != null)
            WriteTextDisassembly();

        if (mScript.MessageScriptSection != null)
            WriteMessageScriptDisassembly();
    }

    private void WriteHeader()
    {
        WriteCommentLine(mHeaderString);
        mWriter.WriteLine();
    }

    private void WriteTextDisassembly()
    {
        mWriter.WriteLine(".text");

        while (mInstructionIndex < mScript.TextSection.Count)
        {
            // Check if there is a possible jump label at the current index
            if (mScript.JumpLabelSection != null)
            {
                foreach (var jump in mScript.JumpLabelSection.Where(x => x.InstructionIndex == mInstructionIndex))
                {
                    mWriter.WriteLine($"{jump.Name}:");
                }
            }

            if (CurrentInstruction.Opcode == Opcode.PROC)
                mWriter.WriteLine();

            WriteInstructionDisassembly();

            if (OpcodeUsesExtendedOperand(CurrentInstruction.Opcode))
            {
                mInstructionIndex += 2;
            }
            else
            {
                mInstructionIndex += 1;
            }
        }

        mWriter.WriteLine();
    }

    private bool OpcodeUsesExtendedOperand(Opcode opcode)
    {
        return opcode == Opcode.PUSHI || opcode == Opcode.PUSHF;
    }

    private void WriteInstructionDisassembly()
    {
        mWriter.Write($"# {mInstructionIndex:D4}:{mInstructionIndex:X4} # ");

        switch (CurrentInstruction.Opcode)
        {
            // extended int operand
            case Opcode.PUSHI:
                mWriter.Write(DisassembleInstructionWithIntOperand(CurrentInstruction, NextInstruction.Value));
                break;

            // extended float operand
            case Opcode.PUSHF:
                mWriter.Write(DisassembleInstructionWithFloatOperand(CurrentInstruction, NextInstruction.Value));
                break;

            // short operand
            case Opcode.PUSHIX:
            case Opcode.PUSHIF:
            case Opcode.POPIX:
            case Opcode.POPFX:
            case Opcode.RUN:
            case Opcode.PUSHIS:
            case Opcode.PUSHLIX:
            case Opcode.PUSHLFX:
            case Opcode.POPLIX:
            case Opcode.POPLFX:
                mWriter.Write(DisassembleInstructionWithShortOperand(CurrentInstruction));
                break;

            // string opcodes
            case Opcode.PUSHSTR:
                mWriter.Write(DisassembleInstructionWithStringReferenceOperand(CurrentInstruction, mScript.StringSection));
                break;

            // branch procedure opcodes
            case Opcode.PROC:
                mWriter.Write(DisassembleInstructionWithLabelReferenceOperand(CurrentInstruction, mScript.ProcedureLabelSection));
                break;

            case Opcode.JUMP:
            case Opcode.CALL:
                mWriter.Write(DisassembleInstructionWithLabelReferenceOperand(CurrentInstruction, mScript.ProcedureLabelSection));
                break;

            // branch jump opcodes                           
            case Opcode.GOTO:
            case Opcode.IF:
                mWriter.Write(DisassembleInstructionWithLabelReferenceOperand(CurrentInstruction, mScript.JumpLabelSection));
                break;

            // branch communicate opcode
            case Opcode.COMM:
                mWriter.Write(DisassembleInstructionWithCommReferenceOperand(CurrentInstruction));
                break;

            // No operands
            case Opcode.PUSHREG:
            case Opcode.END:
            case Opcode.ADD:
            case Opcode.SUB:
            case Opcode.MUL:
            case Opcode.DIV:
            case Opcode.MINUS:
            case Opcode.NOT:
            case Opcode.OR:
            case Opcode.AND:
            case Opcode.EQ:
            case Opcode.NEQ:
            case Opcode.S:
            case Opcode.L:
            case Opcode.SE:
            case Opcode.LE:
            case Opcode.POPREG:
                mWriter.Write(DisassembleInstructionWithNoOperand(CurrentInstruction));
                break;

            default:
                throw new InvalidOperationException($"Unknown opcode {CurrentInstruction.Opcode}");
        }

        mWriter.WriteLine();
    }

    private void WriteMessageScriptDisassembly()
    {
        mWriter.WriteLine(".msg");

        using (var messageScriptDecompiler = new MessageScriptDecompiler(mWriter))
        {
            messageScriptDecompiler.Decompile(MessageScript.FromBinary(mScript.MessageScriptSection));
        }
    }

    public static string DisassembleInstructionWithNoOperand(BinaryInstruction instruction)
    {
        if (instruction.OperandUShort != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(instruction.OperandUShort), $"{instruction.Opcode} should not have any operands");
        }

        return $"{instruction.Opcode}";
    }

    public static string DisassembleInstructionWithIntOperand(BinaryInstruction instruction, BinaryInstruction operand)
    {
        return $"{instruction.Opcode}\t{operand.OperandUInt:X8}";
    }

    public static string DisassembleInstructionWithFloatOperand(BinaryInstruction instruction, BinaryInstruction operand)
    {
        return $"{instruction.Opcode}\t\t{operand.OperandFloat.ToString("0.00#####", CultureInfo.InvariantCulture)}f";
    }

    public static string DisassembleInstructionWithShortOperand(BinaryInstruction instruction)
    {
        return $"{instruction.Opcode}\t{instruction.OperandUShort:X4}";
    }

    public static string DisassembleInstructionWithStringReferenceOperand(BinaryInstruction instruction, IList<byte> stringTable)
    {
        string value = string.Empty;
        for (int i = instruction.OperandUShort; i < stringTable.Count; i++)
        {
            if (stringTable[i] == 0)
                break;

            value += (char)stringTable[i];
        }

        return $"{instruction.Opcode}\t\"{value}\"";
    }

    public static string DisassembleInstructionWithLabelReferenceOperand(BinaryInstruction instruction, IList<BinaryLabel> labels)
    {
        if (instruction.OperandUShort >= labels.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(instruction.OperandUShort), $"No label for label reference id {instruction.OperandUShort} present in {nameof(labels)}");
        }

        return $"{instruction.Opcode}\t\t{labels[instruction.OperandUShort].Name}";
    }

    public static string DisassembleInstructionWithCommReferenceOperand(BinaryInstruction instruction)
    {
        return $"{instruction.Opcode}\t\t{instruction.OperandUShort:X4}";
    }

    public void Dispose()
    {
        Dispose(true);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (mDisposed)
            return;

        mWriter.Dispose();
        mDisposed = true;
    }
}
