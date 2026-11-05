using Newtonsoft.Json;
using System;
using System.Reflection;
using QFramework;

/// <summary>
/// BindableProperty<T> 专用转换器
/// 序列化：输出 Value
/// 反序列化：构造BindableProperty实例，并把json值赋值给Value
/// </summary>
public class BindablePropertyJsonConverter : JsonConverter
{
    // 判断是否可以转换该类型
    public override bool CanConvert(Type objectType)
    {
        if (!objectType.IsGenericType) return false;
        var genericTypeDef = objectType.GetGenericTypeDefinition();
        return genericTypeDef == typeof(BindableProperty<>);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        // 获取内部 T 类型
        Type valueType = objectType.GetGenericArguments()[0];
        object rawValue = serializer.Deserialize(reader, valueType);

        // new BindableProperty<T>(rawValue)
        ConstructorInfo ctor = objectType.GetConstructor(new[] { valueType });
        var bindableObj = ctor.Invoke(new[] { rawValue });

        return bindableObj;
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }
        // 反射拿到 Value 属性
        PropertyInfo prop = value.GetType().GetProperty("Value");
        object innerVal = prop.GetValue(value);
        serializer.Serialize(writer, innerVal);
    }
}