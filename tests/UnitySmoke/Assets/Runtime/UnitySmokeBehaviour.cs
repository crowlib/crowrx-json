using System;
using UnityEngine;

namespace CrowRx.Json.UnitySmoke
{
    public sealed class UnitySmokeBehaviour : MonoBehaviour
    {
        private void Start()
        {
            try
            {
                RunAssertions();
                Debug.Log("CROWRX_JSON_UNITY_SMOKE_OK");
                Application.Quit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Application.Quit(1);
            }
        }

        private static void RunAssertions()
        {
            var source = new MutableDto { Id = 42, Name = "unity" };
            string json = JsonSerializer.Serialize(source);
            MutableDto result = JsonSerializer.Deserialize<MutableDto>(json);
            if (result == null || result.Id != source.Id || result.Name != source.Name)
            {
                throw new InvalidOperationException("Mutable DTO round-trip failed.");
            }

            var immutable = JsonSerializer.Deserialize<ImmutableDto>("{\"Id\":7,\"Name\":\"ctor\"}");
            if (immutable == null || immutable.Id != 7 || immutable.Name != "ctor")
            {
                throw new InvalidOperationException("Constructor binding failed.");
            }

            JsonSerializerOptions options = new JsonSerializerOptions(
                    propertyNameCaseInsensitive: true,
                    rejectUnknownMembers: true,
                    allowNumericEnums: true)
                .WithConverter(new TokenConverter());
            var optionDto = JsonSerializer.Deserialize<OptionDto>("{\"token\":5,\"mode\":2}", options);
            if (optionDto == null || optionDto.Token.Value != 5 || optionDto.Mode != SmokeMode.Two)
            {
                throw new InvalidOperationException("Options/converter path failed.");
            }

            if (!JsonSerializer.TryOverwrite("{\"Name\":\"updated\"}", source, out JsonError error, options: options) ||
                error.Category != JsonErrorCategory.None ||
                source.Id != 42 ||
                source.Name != "updated")
            {
                throw new InvalidOperationException("Overwrite path failed.");
            }
        }

        public sealed class MutableDto
        {
            public int Id;
            public string Name = string.Empty;
        }

        public sealed class ImmutableDto
        {
            public int Id { get; }
            public string Name { get; }

            public ImmutableDto(int Id, string Name)
            {
                this.Id = Id;
                this.Name = Name;
            }
        }

        public sealed class OptionDto
        {
            public Token Token { get; set; }
            public SmokeMode Mode { get; set; }
        }

        public readonly struct Token
        {
            public int Value { get; }
            public Token(int value) => Value = value;
        }

        public enum SmokeMode
        {
            One = 1,
            Two = 2,
        }

        private sealed class TokenConverter : IJsonConverter<Token>
        {
            public string Write(Token value) => value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            public Token Read(string json) => new Token(int.Parse(json, System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}
