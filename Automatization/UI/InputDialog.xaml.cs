using Automatization.Secrets;
using System.Windows;

namespace Automatization.UI
{
    public partial class InputDialog : Window
    {
        public PasswordDialogResult Result { get; private set; }

        public string? Password { get; private set; }
        private string? _adminPassword;

        public InputDialog()
        {
            InitializeComponent();
            _adminPassword = AdminSecret.GetAdminPassword();
            _ = PasswordInput.Focus();
            Result = PasswordDialogResult.Cancelled;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            string enteredPassword = PasswordInput.Password;

            if (_adminPassword != null && enteredPassword == _adminPassword)
            {
                Password = enteredPassword;
                Result = PasswordDialogResult.Correct;
                DialogResult = true;
            }
            else
            {
                ErrorMessage.Text = "Incorrect password.";
                ErrorMessage.Visibility = Visibility.Visible;
                Result = PasswordDialogResult.Incorrect;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Password = null;
            Result = PasswordDialogResult.Cancelled;
            DialogResult = false;
        }
    }
}
