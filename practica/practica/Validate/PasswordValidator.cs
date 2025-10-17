using System.ComponentModel.DataAnnotations;

public static class PasswordValidator
{
    public static void Validate(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ValidationException("������ �� ����� ���� ������");

        if (password.Length < 6)
            throw new ValidationException("������ ������ ��������� ������� 6 ��������");
    }
}
