using System.ComponentModel.DataAnnotations;
using System.IO;

namespace FERCPlugin.Main.Host;
public class IsEnumAttribute : ValidationAttribute {
    private string _constructorValidationErr =
        "[IsEnum]: атрибут может быть применен только с типом enum или enum[]";
    private string _invalidValueErr = "Некорректное значение поля";

    private Type? _enum { get; }
    private Type[] _enumArr { get; } = [];

    public IsEnumAttribute(Type @enum) {
        if (!@enum.IsEnum)
            throw new InvalidDataException(_constructorValidationErr);
        _enum = @enum;
    }
    public IsEnumAttribute(Type[] enumArr) {
        if (!enumArr.GetType().IsArray)
            throw new InvalidDataException(_constructorValidationErr);
        if (!enumArr.Any() || enumArr.Any(e => !e.IsEnum))
            throw new InvalidDataException(_constructorValidationErr);

        _enumArr = enumArr;
    }

    protected override ValidationResult IsValid(object? value,
                                                ValidationContext context) {
        if (value is null)
            return ValidationResult.Success;

        if (value is not string) {
            var result = new ValidationResult(_invalidValueErr, [context.MemberName]);
            return result;
        }

        if (_enum is not null)
            return Enum.IsDefined(_enum, value)
                ? ValidationResult.Success
                : new ValidationResult(_invalidValueErr, [context.MemberName]);

        // all below for enums arr
        return _enumArr.Any(e => Enum.IsDefined(e, value))
            ? ValidationResult.Success
            : new ValidationResult(_invalidValueErr, [context.MemberName]);
    }
}
