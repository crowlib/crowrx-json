using System.Collections.Generic;
using UnityEngine;


namespace CrowRx.Json.Sample
{
    using Utility;


    public class TestJson : MonoBehaviour
    {
        public enum DummyEnum
        {
            Zero,
            One,
            Two,
            Three,
        }

        public class DummyElementClass
        {
            public class InternalDummyElementClass
            {
                public sbyte SByteValue;
                public byte ByteValue;

                public short ShortValue;
                public ushort UShortValue;

                public int IntValue;
                public uint UIntValue;

                public long LongValue;
                public ulong ULongValue;

                public bool BoolValue;

                public string StringValue;

                public float FloatValue;

                public double DoubleValue;
            }

            public List<InternalDummyElementClass> InternalDummyElementClasses;
            public DummyElementStruct.InternalDummyElementStruct[] InternalDummyElementStructs;

            public Dictionary<DummyEnum, DummyDictionaryElementClass> EnumKeyDictionary;

            public int? NullableInt;
            private DummyElementStruct.InternalDummyElementStruct? NullableStruct;
        }

        public class DummyDictionaryElementClass
        {
            public int IntValue;

            public DummyElementClass.InternalDummyElementClass InternalDummyElementClass;
        }

        public struct DummyElementStruct
        {
            public struct InternalDummyElementStruct
            {
                public sbyte SByteValue;
                public byte ByteValue;

                public short ShortValue;
                public ushort UShortValue;

                public int IntValue;
                public uint UIntValue;

                public long LongValue;
                public ulong ULongValue;

                public bool BoolValue;

                public string StringValue;

                public float FloatValue;

                public double DoubleValue;
            }

            public List<DummyElementClass.InternalDummyElementClass> InternalDummyElementClasses;
            public InternalDummyElementStruct[] InternalDummyElementStructs;

            public Dictionary<DummyEnum, DummyDictionaryElementClass> EnumKeyDictionary;
        }

        public struct DummyStruct
        {
            public List<DummyElementClass> DummyElementClasses;

            public Dictionary<string, DummyElementClass> StringKeyDictionary;
            public Dictionary<int, DummyElementClass> IntKeyDictionary;
            public Dictionary<DummyEnum, DummyElementClass> EnumKeyDictionary;
        }

        // Start is called before the first frame update
        void Start()
        {
            DummyElementClass dummyElementClass = new()
            {
                InternalDummyElementClasses = new()
                {
                    new()
                    {
                        SByteValue = -1,
                        ByteValue = 1,

                        ShortValue = -10,
                        UShortValue = 10,

                        IntValue = -100,
                        UIntValue = 100,

                        LongValue = -1000,
                        ULongValue = 1000,

                        BoolValue = true,

                        StringValue = "1st",

                        FloatValue = 1f,

                        DoubleValue = 10.0
                    },

                    new()
                    {
                        SByteValue = -2,
                        ByteValue = 2,

                        ShortValue = -20,
                        UShortValue = 20,

                        IntValue = -200,
                        UIntValue = 200,

                        LongValue = -2000,
                        ULongValue = 2000,

                        BoolValue = false,

                        StringValue = "2nd",

                        FloatValue = 2f,

                        DoubleValue = 20.0
                    },
                },

                InternalDummyElementStructs = new DummyElementStruct.InternalDummyElementStruct[]
                {
                    new()
                    {
                        SByteValue = -1,
                        ByteValue = 1,

                        ShortValue = -10,
                        UShortValue = 10,

                        IntValue = -100,
                        UIntValue = 100,

                        LongValue = -1000,
                        ULongValue = 1000,

                        BoolValue = true,

                        StringValue = "1st",

                        FloatValue = 1f,

                        DoubleValue = 10.0
                    },

                    new()
                    {
                        SByteValue = -2,
                        ByteValue = 2,

                        ShortValue = -20,
                        UShortValue = 20,

                        IntValue = -200,
                        UIntValue = 200,

                        LongValue = -2000,
                        ULongValue = 2000,

                        BoolValue = false,

                        StringValue = "2nd",

                        FloatValue = 2f,

                        DoubleValue = 20.0
                    },
                },

                EnumKeyDictionary = new(),

                NullableInt = 9999,

                //NullableStruct = null,
            };

            dummyElementClass.EnumKeyDictionary.Add(DummyEnum.One, new()
            {
                IntValue = 12345,
                InternalDummyElementClass = new()
                {
                    SByteValue = -12,
                    ByteValue = 12,

                    ShortValue = -102,
                    UShortValue = 102,

                    IntValue = -1002,
                    UIntValue = 1002,

                    LongValue = -10002,
                    ULongValue = 10002,

                    BoolValue = true,

                    StringValue = "1st-EnumKeyDictionary",

                    FloatValue = 12f,

                    DoubleValue = 10.02
                }
            });

            string json = dummyElementClass.ToJson();

            Log.Info($"json : {json}");

            if (json.TryConvertFromJson<DummyElementClass>(out var result))
            {
                var secondJson = result.ToJson();

                Log.Info($"secondJson : {secondJson}");

                if (json.Equals(secondJson) == false)
                    Log.Error("result is difference.");

                if (secondJson.TryConvertFromJsonOverwrite(ref dummyElementClass))
                {
                    var thirdJson = dummyElementClass.ToJson();

                    Log.Info($"thirdJson : {thirdJson}");

                    if (secondJson.Equals(thirdJson) == false)
                        Log.Error("result is difference.");

                    var lastDummyElementClass = thirdJson.ConvertFromJson<DummyElementClass>();

                    var forthJason = lastDummyElementClass.ToJson();

                    Log.Info($"forthJason : {forthJason}");

                    if (thirdJson.Equals(forthJason) == false)
                        Log.Error("result is difference.");
                }
            }
        }
    }
}