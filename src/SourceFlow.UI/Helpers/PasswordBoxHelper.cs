using System.Security;
using System.Windows;
using System.Windows.Controls;

namespace SourceFlow.UI.Helpers;

/// <summary>
/// PasswordBoxのMVVMバインディングをサポートするヘルパークラス
/// </summary>
public static class PasswordBoxHelper
{
    public static readonly DependencyProperty BoundPasswordProperty =
        DependencyProperty.RegisterAttached(
            "BoundPassword",
            typeof(string),
            typeof(PasswordBoxHelper),
            new PropertyMetadata(string.Empty, OnBoundPasswordChanged));

    public static readonly DependencyProperty BindPasswordProperty =
        DependencyProperty.RegisterAttached(
            "BindPassword",
            typeof(bool),
            typeof(PasswordBoxHelper),
            new PropertyMetadata(false, OnBindPasswordChanged));

    private static readonly DependencyProperty UpdatingPasswordProperty =
        DependencyProperty.RegisterAttached(
            "UpdatingPassword",
            typeof(bool),
            typeof(PasswordBoxHelper),
            new PropertyMetadata(false));

    public static void SetBindPassword(DependencyObject dp, bool value)
    {
        dp.SetValue(BindPasswordProperty, value);
    }

    public static bool GetBindPassword(DependencyObject dp)
    {
        return (bool)dp.GetValue(BindPasswordProperty);
    }

    public static string GetBoundPassword(DependencyObject dp)
    {
        return (string)dp.GetValue(BoundPasswordProperty);
    }

    public static void SetBoundPassword(DependencyObject dp, string value)
    {
        dp.SetValue(BoundPasswordProperty, value);
    }

    private static bool GetUpdatingPassword(DependencyObject dp)
    {
        return (bool)dp.GetValue(UpdatingPasswordProperty);
    }

    private static void SetUpdatingPassword(DependencyObject dp, bool value)
    {
        dp.SetValue(UpdatingPasswordProperty, value);
    }

    private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PasswordBox passwordBox)
        {
            passwordBox.PasswordChanged -= HandlePasswordChanged;

            if (!GetUpdatingPassword(passwordBox))
            {
                passwordBox.Password = (string)e.NewValue;
            }

            passwordBox.PasswordChanged += HandlePasswordChanged;
        }
    }

    private static void OnBindPasswordChanged(DependencyObject dp, DependencyPropertyChangedEventArgs e)
    {
        if (dp is PasswordBox passwordBox)
        {
            bool wasBound = (bool)(e.OldValue);
            bool needToBind = (bool)(e.NewValue);

            if (wasBound)
            {
                passwordBox.PasswordChanged -= HandlePasswordChanged;
            }

            if (needToBind)
            {
                passwordBox.PasswordChanged += HandlePasswordChanged;
            }
        }
    }

    private static void HandlePasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox)
        {
            SetUpdatingPassword(passwordBox, true);
            SetBoundPassword(passwordBox, passwordBox.Password);
            SetUpdatingPassword(passwordBox, false);
        }
    }

    /// <summary>
    /// パスワードをSecureStringに変換（セキュリティ強化用）
    /// </summary>
    public static SecureString ConvertToSecureString(string password)
    {
        if (password == null)
            return new SecureString();

        var secureString = new SecureString();
        foreach (char c in password)
        {
            secureString.AppendChar(c);
        }
        secureString.MakeReadOnly();
        return secureString;
    }

    /// <summary>
    /// SecureStringを通常の文字列に変換（必要時のみ使用）
    /// </summary>
    public static string ConvertToString(SecureString secureString)
    {
        if (secureString == null || secureString.Length == 0)
            return string.Empty;

        var ptr = System.Runtime.InteropServices.Marshal.SecureStringToBSTR(secureString);
        try
        {
            return System.Runtime.InteropServices.Marshal.PtrToStringBSTR(ptr);
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.ZeroFreeBSTR(ptr);
        }
    }
}