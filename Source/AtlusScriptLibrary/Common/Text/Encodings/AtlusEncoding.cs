﻿using System;
using System.Collections.Generic;
using System.Text;

namespace AtlusScriptLibrary.Common.Text.Encodings
{
    public abstract class AtlusEncoding : Encoding
    {
        /// <summary>
        /// Offset from start of glyph range to start of the char table.
        /// </summary>
        private const int CHAR_TO_GLYPH_INDEX_OFFSET = 0x60;

        /// <summary>
        /// Size of a single glyph table.
        /// </summary>
        private const int GLYPH_TABLE_SIZE = 0x80;

        /// <summary>
        /// The range 0-based range of an ascii character index.
        /// </summary>
        private const int ASCII_RANGE = 0x7F;

        /// <summary>
        /// The high bit serves as a marker for a table index.
        /// </summary>
        private const int GLYPH_TABLE_INDEX_MARKER = 0x80;

        private static bool sIsInitialized;

        private static Dictionary<char, CodePoint> sCharToCodePoint;

        private static Dictionary<CodePoint, char> sCodePointToChar;

        // Ease of use accessors
        private static Persona3Encoding sPersona3;
        private static Persona4Encoding sPersona4;
        private static Persona5Encoding sPersona5;
        private static Persona5ChiEncoding sPersona5Chi;

        public static AtlusEncoding Persona3 => sPersona3 ?? ( sPersona3 = new Persona3Encoding() );
        public static AtlusEncoding Persona4 => sPersona4 ?? ( sPersona4 = new Persona4Encoding() );
        public static AtlusEncoding Persona5 => sPersona5 ?? ( sPersona5 = new Persona5Encoding() );
        public static AtlusEncoding Persona5Chi => sPersona5Chi ?? ( sPersona5Chi = new Persona5ChiEncoding() );

        public static AtlusEncoding GetByName( string name )
        {
            // Normalize name
            name = name.ToLowerInvariant().Replace( " ", "" );

            switch ( name )
            {
                case "p3":
                case "persona3":
                    return Persona3;

                case "p4":
                case "persona4":
                    return Persona4;

                case "p5":
                case "persona5":
                    return Persona5;

                case "p5chi":
                case "persona5chi":
                    return Persona5Chi;

                default:
                    throw new ArgumentException( nameof( name ) );
            }
        }

        public override int GetByteCount( char[] chars, int index, int count )
        {
            int byteCount = 0;
            for ( int i = index; i < count; i++ )
            {
                if ( chars[i] <= ASCII_RANGE )
                    byteCount += 1;
                else
                    byteCount += 2;
            }

            return byteCount;
        }

        public override int GetBytes( char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex )
        {
            int byteCount = 0;

            for ( ; charIndex < charCount; charIndex++ )
            {
                CodePoint codePoint;
                var c = chars[ charIndex ];

                try
                {
                    codePoint = sCharToCodePoint[c];
                }
                catch ( KeyNotFoundException )
                {
                    throw new UnsupportedCharacterException( EncodingName, c );
                }

                if ( codePoint.HighSurrogate == 0 )
                {
                    bytes[byteIndex++] = codePoint.LowSurrogate;
                    byteCount += 1;
                }
                else
                {
                    bytes[byteIndex++] = codePoint.HighSurrogate;
                    bytes[byteIndex++] = codePoint.LowSurrogate;
                    byteCount += 2;
                }

            }

            return byteCount;
        }

        public override int GetCharCount( byte[] bytes, int index, int count )
        {
            int charCount = 0;
            for ( ; index < count; ++index, ++charCount )
            {
                if ( ( bytes[index] & GLYPH_TABLE_INDEX_MARKER ) == GLYPH_TABLE_INDEX_MARKER )
                {
                    ++index;
                }
            }

            return charCount;
        }

        public override int GetChars( byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex )
        {
            return GetCharsImpl( bytes, byteIndex, byteCount, chars, charIndex, out _ );
        }

        private int GetCharsImpl( byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex, out bool hasUndefinedChars )
        {
            int charCount = 0;
            hasUndefinedChars = false;

            for ( ; byteIndex < byteCount; byteIndex++ )
            {
                CodePoint cp;
                if ( ( bytes[byteIndex] & GLYPH_TABLE_INDEX_MARKER ) == GLYPH_TABLE_INDEX_MARKER )
                {
                    cp.HighSurrogate = bytes[byteIndex++];
                }
                else
                {
                    cp.HighSurrogate = 0;
                }

                cp.LowSurrogate = bytes[byteIndex];

                if ( !sCodePointToChar.TryGetValue( cp, out var c ) || c == '\0' )
                    hasUndefinedChars = true;

                chars[charIndex++] = c;
                charCount++;
            }

            return charCount;
        }

        public override int GetMaxByteCount( int charCount )
        {
            return charCount * 2;
        }

        public override int GetMaxCharCount( int byteCount )
        {
            return byteCount;
        }

        public bool TryGetString( byte[] bytes, out string value )
        {
            var chars = new char[GetMaxCharCount( bytes.Length )];
            GetCharsImpl( bytes, 0, bytes.Length, chars, 0, out bool hasUndefinedChars );

            if ( hasUndefinedChars )
            {
                value = null;
                return false;
            }

            value = new string( chars );
            return true;
        }

        protected abstract char[] CharTable { get; }

        protected AtlusEncoding()
        {
            if ( sIsInitialized )
                return;

            // build character to codepoint table
            sCharToCodePoint = new Dictionary<char, CodePoint>( CharTable.Length );

            // add the ascii range seperately
            for ( int charIndex = 0; charIndex < ASCII_RANGE + 1; charIndex++ )
            {
                if ( !sCharToCodePoint.ContainsKey( CharTable[charIndex] ) )
                    sCharToCodePoint[CharTable[charIndex]] = new CodePoint( 0, ( byte )charIndex );
            }

            // add extended characters, but don't re-include the ascii range
            for ( int charIndex = ASCII_RANGE + 1; charIndex < CharTable.Length; charIndex++ )
            {
                int glyphIndex = charIndex + CHAR_TO_GLYPH_INDEX_OFFSET;
                int tableIndex = ( glyphIndex / GLYPH_TABLE_SIZE ) - 1;
                int tableRelativeIndex = glyphIndex - ( tableIndex * GLYPH_TABLE_SIZE );

                if ( !sCharToCodePoint.ContainsKey( CharTable[charIndex] ) )
                    sCharToCodePoint[CharTable[charIndex]] = new CodePoint( ( byte )( GLYPH_TABLE_INDEX_MARKER | tableIndex ), ( byte )( tableRelativeIndex ) );
            }

            // build code point to character lookup table
            sCodePointToChar = new Dictionary<CodePoint, char>( CharTable.Length );

            // add the ascii range seperately
            for ( int charIndex = 0; charIndex < ASCII_RANGE + 1; charIndex++ )
            {
                sCodePointToChar[new CodePoint( 0, ( byte )charIndex )] = CharTable[charIndex];
            }

            // add extended characters, and make sure to include the ascii range again due to overlap
            for ( int charIndex = 0x20; charIndex < CharTable.Length; charIndex++ )
            {
                int glyphIndex = charIndex + CHAR_TO_GLYPH_INDEX_OFFSET;
                int tableIndex = ( glyphIndex / GLYPH_TABLE_SIZE ) - 1;
                int tableRelativeIndex = glyphIndex - ( tableIndex * GLYPH_TABLE_SIZE );

                sCodePointToChar[new CodePoint( ( byte )( GLYPH_TABLE_INDEX_MARKER | tableIndex ), ( byte )( tableRelativeIndex ) )] = CharTable[charIndex];
            }

            sIsInitialized = true;
        }
    }
}
