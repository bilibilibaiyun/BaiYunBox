using Jint;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace BaiYunBox.Crawler;

/// <summary>
/// dr_py 的 muban 动态对象：支持任意深层赋值（muban.a.b.c = 'x'），
/// 自动创建中间对象，读回后供模板选择器覆盖。
/// </summary>
public sealed class MubanObject : ObjectInstance
{
    private readonly Dictionary<string, object?> _values = new();
    private readonly Dictionary<string, MubanObject> _children = new();

    public MubanObject(Engine engine) : base(engine) { }

    public override JsValue Get(JsValue property, JsValue receiver)
    {
        var key = property.AsString();
        if (_children.TryGetValue(key, out var child))
            return child;
        if (_values.TryGetValue(key, out var val))
            return JsValue.FromObject(Engine, val);
        // 自动创建中间对象
        var newChild = new MubanObject(Engine);
        _children[key] = newChild;
        return newChild;
    }

    public override bool Set(JsValue property, JsValue value, JsValue receiver)
    {
        var key = property.AsString();
        _children.Remove(key);
        _values[key] = value.IsUndefined() || value.IsNull() ? null : value.ToObject();
        return true;
    }

    public override bool HasProperty(JsValue property)
    {
        var key = property.AsString();
        return _values.ContainsKey(key) || _children.ContainsKey(key);
    }

    public override List<JsValue> GetOwnPropertyKeys(Types types = Types.String | Types.Symbol)
    {
        var keys = new List<JsValue>();
        foreach (var k in _values.Keys) keys.Add(k);
        foreach (var k in _children.Keys)
            if (!_values.ContainsKey(k)) keys.Add(k);
        return keys;
    }

    /// <summary>展平为 "a.b.c" → 值 的映射。</summary>
    public Dictionary<string, object?> Flatten()
    {
        var result = new Dictionary<string, object?>();
        FlattenInto(result, "");
        return result;
    }

    private void FlattenInto(Dictionary<string, object?> result, string prefix)
    {
        foreach (var kv in _values)
        {
            var name = prefix.Length == 0 ? kv.Key : prefix + "." + kv.Key;
            result[name] = kv.Value;
        }
        foreach (var kv in _children)
        {
            var name = prefix.Length == 0 ? kv.Key : prefix + "." + kv.Key;
            kv.Value.FlattenInto(result, name);
        }
    }
}
