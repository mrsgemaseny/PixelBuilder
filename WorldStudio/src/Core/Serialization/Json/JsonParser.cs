using System;
using System.Globalization;
using System.Text;

namespace MiniLife.WorldStudio.Serialization.Json
{
    public sealed class JsonParseException : Exception
    {
        public JsonParseException(string message, int line, int column)
            : base(message + " (строка " + line + ", столбец " + column + ")")
        {
            Line = line;
            Column = column;
        }

        public int Line { get; private set; }
        public int Column { get; private set; }
    }

    // Строгий парсер RFC 8259. Отличия от JavaScriptSerializer, нужные инструменту:
    // порядок ключей сохраняется, повтор ключа - ошибка (иначе молча теряется клетка),
    // числа хранятся текстом, ошибка указывает строку и столбец.
    public sealed class JsonParser
    {
        private const int MaxDepth = 128;

        private readonly string _text;
        private int _pos;
        private int _line = 1;
        private int _lineStart;
        private int _depth;

        private JsonParser(string text)
        {
            _text = text;
        }

        public static JsonNode Parse(string text)
        {
            if (text == null) throw new ArgumentNullException("text");
            var parser = new JsonParser(text);
            if (text.Length > 0 && text[0] == '﻿') parser._pos = 1; // BOM допускается
            parser.SkipWhitespace();
            if (parser.AtEnd) parser.Fail("Пустой файл");
            JsonNode root = parser.ParseValue();
            parser.SkipWhitespace();
            if (!parser.AtEnd) parser.Fail("Лишние символы после конца JSON");
            return root;
        }

        private bool AtEnd { get { return _pos >= _text.Length; } }
        private int Column { get { return _pos - _lineStart + 1; } }

        private void Fail(string message)
        {
            throw new JsonParseException(message, _line, Column);
        }

        private JsonNode ParseValue()
        {
            if (AtEnd) Fail("Неожиданный конец файла");
            int line = _line, column = Column;
            JsonNode node;
            char c = _text[_pos];
            if (c == '{') node = ParseObject();
            else if (c == '[') node = ParseArray();
            else if (c == '"') node = new JsonString(ParseString());
            else if (c == '-' || (c >= '0' && c <= '9')) node = ParseNumber();
            else if (Match("true")) node = new JsonBool(true);
            else if (Match("false")) node = new JsonBool(false);
            else if (Match("null")) node = new JsonNull();
            else
            {
                Fail("Неожиданный символ '" + c + "'");
                return null;
            }
            node.Line = line;
            node.Column = column;
            return node;
        }

        private JsonObject ParseObject()
        {
            Enter();
            var obj = new JsonObject();
            _pos++; // {
            SkipWhitespace();
            if (Peek() == '}') { _pos++; Leave(); return obj; }
            while (true)
            {
                SkipWhitespace();
                if (Peek() != '"') Fail("Ожидался ключ в кавычках");
                int keyLine = _line, keyColumn = Column;
                string key = ParseString();
                if (obj.Contains(key))
                    throw new JsonParseException("Повтор ключа \"" + key + "\"", keyLine, keyColumn);
                SkipWhitespace();
                Expect(':');
                SkipWhitespace();
                obj.Add(key, ParseValue());
                SkipWhitespace();
                char c = Peek();
                if (c == ',') { _pos++; continue; }
                if (c == '}') { _pos++; break; }
                Fail("Ожидалась ',' или '}'");
            }
            Leave();
            return obj;
        }

        private JsonArray ParseArray()
        {
            Enter();
            var arr = new JsonArray();
            _pos++; // [
            SkipWhitespace();
            if (Peek() == ']') { _pos++; Leave(); return arr; }
            while (true)
            {
                SkipWhitespace();
                arr.Add(ParseValue());
                SkipWhitespace();
                char c = Peek();
                if (c == ',') { _pos++; continue; }
                if (c == ']') { _pos++; break; }
                Fail("Ожидалась ',' или ']'");
            }
            Leave();
            return arr;
        }

        private string ParseString()
        {
            _pos++; // "
            var sb = new StringBuilder();
            while (true)
            {
                if (AtEnd) Fail("Незакрытая строка");
                char c = _text[_pos];
                if (c == '"') { _pos++; return sb.ToString(); }
                if (c < 0x20) Fail("Управляющий символ внутри строки");
                if (c != '\\') { sb.Append(c); _pos++; continue; }
                _pos++;
                if (AtEnd) Fail("Незакрытая строка");
                char e = _text[_pos++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (_pos + 4 > _text.Length) Fail("Неполная escape-последовательность \\u");
                        string hex = _text.Substring(_pos, 4);
                        int code;
                        if (!int.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out code))
                            Fail("Некорректная escape-последовательность \\u" + hex);
                        sb.Append((char)code);
                        _pos += 4;
                        break;
                    default:
                        _pos--;
                        Fail("Неизвестная escape-последовательность \\" + e);
                        break;
                }
            }
        }

        private JsonNumber ParseNumber()
        {
            int start = _pos;
            if (Peek() == '-') _pos++;
            if (Peek() == '0') _pos++;
            else if (IsDigit(Peek())) { while (IsDigit(Peek())) _pos++; }
            else Fail("Некорректное число");
            if (Peek() == '.')
            {
                _pos++;
                if (!IsDigit(Peek())) Fail("После точки ожидалась цифра");
                while (IsDigit(Peek())) _pos++;
            }
            if (Peek() == 'e' || Peek() == 'E')
            {
                _pos++;
                if (Peek() == '+' || Peek() == '-') _pos++;
                if (!IsDigit(Peek())) Fail("В экспоненте ожидалась цифра");
                while (IsDigit(Peek())) _pos++;
            }
            return new JsonNumber(_text.Substring(start, _pos - start));
        }

        private static bool IsDigit(char c) { return c >= '0' && c <= '9'; }

        private char Peek() { return AtEnd ? '\0' : _text[_pos]; }

        private bool Match(string word)
        {
            if (string.CompareOrdinal(_text, _pos, word, 0, word.Length) != 0) return false;
            _pos += word.Length;
            return true;
        }

        private void Expect(char c)
        {
            if (Peek() != c) Fail("Ожидался символ '" + c + "'");
            _pos++;
        }

        private void Enter()
        {
            if (++_depth > MaxDepth) Fail("Слишком глубокая вложенность");
        }

        private void Leave() { _depth--; }

        private void SkipWhitespace()
        {
            while (!AtEnd)
            {
                char c = _text[_pos];
                if (c == '\n') { _pos++; _line++; _lineStart = _pos; }
                else if (c == ' ' || c == '\t' || c == '\r') _pos++;
                else break;
            }
        }
    }
}
