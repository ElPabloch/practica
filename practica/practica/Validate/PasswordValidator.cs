using System.ComponentModel.DataAnnotations;

public static class PasswordValidator
{
    public static void Validate(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ValidationException("Пароль не может быть пустым");

        if (password.Length < 6)
            throw new ValidationException("Пароль должен содержать минимум 6 символов");
    }
}
