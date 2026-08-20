using System.Globalization;
using Momoka.Home.Level;
using Momoka.Home.Entities;
using Momoka.Home.Entities.Properties;
namespace Momoka.Home.Level.Commands;

/// <summary>
/// 通用属性编辑命令：设置 / 清除一个属性的值（属性不存在 → 失败，语义同
/// <see cref="PropertySourceExtensions.SetValue"/>）。入值按属性类型强转
/// （协议 JSON 标量 → 盒装值的宽松转换：long→int、double→float 等）。
/// <c>createIfMissing</c> = 按需补建（重涂 texture 等模板外操作）。
/// </summary>
public sealed class SetPropertyCommand : IEditorCommand
{
    private readonly Guid _entityId;
    private readonly string _propertyName;
    private readonly object? _value;
    private readonly bool _createIfMissing;

    public SetPropertyCommand(Guid entityId, string propertyName, object? value, bool createIfMissing = false)
    {
        _entityId = entityId;
        _propertyName = propertyName;
        _value = value;
        _createIfMissing = createIfMissing;
    }

    public bool Execute(EditorSession session, out ChangeSet changes)
    {
        changes = new ChangeSet();
        var entity = session.Layout.Find(_entityId);
        if (entity is null)
            return false;

        var property = entity.Properties.FirstOrDefault(p => p.Name == _propertyName);
        try
        {
            if (_value is null)
            {
                entity.ClearValue(_propertyName);
            }
            else if (property is not null)
            {
                entity.SetValue(_propertyName, Coerce(_value, property.ValueType));
            }
            else if (_createIfMissing)
            {
                var created = CreateForValue(_propertyName, _value);
                entity.AddProperty(created);
                entity.SetValue(_propertyName, Coerce(_value, created.ValueType));
            }
            else
            {
                return false; // 属性不存在且不按需补建
            }
        }
        catch (KeyNotFoundException)
        {
            return false;
        }

        changes.Modified(entity);
        return true;
    }

    /// <summary>按入值类型补建属性（texture 等模板外操作）——默认值取类型缺省，
    /// 实际值由随后的 SetValue 写入（保证"清除 → 回默认"语义正确）。</summary>
    private static Property CreateForValue(string name, object value) => value switch
    {
        bool => new BooleanProperty(name),
        string => new StringProperty(name),
        int => new IntProperty(name),
        float => new FloatProperty(name),
        _ => throw new InvalidOperationException($"Cannot create property for value type '{value.GetType().Name}'."),
    };

    /// <summary>把宽松入值强转为属性值类型（long→int、double→float、boxed enum 等）。</summary>
    private static object Coerce(object value, Type? target)
    {
        if (target is null || target.IsInstanceOfType(value))
            return value;
        if (target == typeof(float))
            return Convert.ToSingle(value, CultureInfo.InvariantCulture);
        if (target == typeof(int))
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        if (target == typeof(bool))
            return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        if (target == typeof(string))
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        if (target.IsEnum)
            return Enum.ToObject(target, Convert.ToInt64(value, CultureInfo.InvariantCulture));
        return value;
    }
}
