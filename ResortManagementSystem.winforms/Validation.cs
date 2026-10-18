using System.Globalization;
using System.Text.RegularExpressions;

namespace ResortManagementSystem.Winforms
{
    // Reusable validation rules. Every rule returns null when the value is fine,
    // or a human-readable error message when it is not.
    internal static class InputRules
    {
        private static readonly Regex PersonNameRegex = new(@"^[\p{L}][\p{L} .'\-]*$", RegexOptions.Compiled);
        private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$", RegexOptions.Compiled);
        private static readonly Regex CodeRegex = new(@"^[A-Za-z0-9_\-]+$", RegexOptions.Compiled);
        private static readonly Regex PlaceRegex = new(@"^[\p{L}\p{N} .,'&()\-#/]+$", RegexOptions.Compiled);

        // Letters only (plus space . ' -). Numbers and symbols are rejected.
        public static string? PersonName(string raw, string label = "Full name")
        {
            var v = raw.Trim();
            if (v.Length == 0) return $"{label} is required.";
            if (v.Any(char.IsDigit)) return $"{label} cannot contain numbers.";
            if (!PersonNameRegex.IsMatch(v)) return $"{label} can only contain letters, spaces, periods, apostrophes and hyphens.";
            if (v.Count(char.IsLetter) < 2) return $"{label} must have at least 2 letters.";
            if (v.Length > 100) return $"{label} is too long (maximum 100 characters).";
            return null;
        }

        // Exactly 11 digits starting with 09, e.g. 09171234567.
        // To accept any 11 digits, delete the StartsWith check below.
        public static string? Phone(string raw, bool required)
        {
            var v = raw.Trim();
            if (v.Length == 0) return required ? "Phone number is required." : null;
            if (v.Any(c => !char.IsDigit(c))) return "Phone number can only contain digits (no letters, spaces or dashes).";
            if (v.Length != 11) return $"Phone number must be exactly 11 digits (you entered {v.Length}).";
            if (!v.StartsWith("09")) return "Phone number must start with 09 (example: 09171234567).";
            return null;
        }

        public static string? Email(string raw)
        {
            var v = raw.Trim();
            if (v.Length == 0) return "Email is required.";
            if (v.Length > 100) return "Email is too long (maximum 100 characters).";
            if (!EmailRegex.IsMatch(v)) return "Enter a valid email address (example: name@example.com).";
            return null;
        }

        public static string? Text(string raw, string label, int min, int max, bool required)
        {
            var v = raw.Trim();
            if (v.Length == 0) return required ? $"{label} is required." : null;
            if (v.Length < min) return $"{label} must be at least {min} characters.";
            if (v.Length > max) return $"{label} is too long (maximum {max} characters).";
            if (!v.Any(char.IsLetterOrDigit)) return $"{label} must contain letters or numbers.";
            return null;
        }

        // Names of places / branches: letters and numbers allowed, but must contain letters.
        public static string? Place(string raw, string label, int min, int max, bool required)
        {
            var v = raw.Trim();
            if (v.Length == 0) return required ? $"{label} is required." : null;
            if (!PlaceRegex.IsMatch(v)) return $"{label} contains characters that are not allowed.";
            if (!v.Any(char.IsLetter)) return $"{label} must contain letters.";
            if (v.Length < min) return $"{label} must be at least {min} characters.";
            if (v.Length > max) return $"{label} is too long (maximum {max} characters).";
            return null;
        }

        // Codes such as promo codes: no spaces, letters / numbers / - / _ only.
        public static string? Code(string raw, string label, int min, int max)
        {
            var v = raw.Trim();
            if (v.Length == 0) return $"{label} is required.";
            if (v.Any(char.IsWhiteSpace)) return $"{label} cannot contain spaces.";
            if (!CodeRegex.IsMatch(v)) return $"{label} can only contain letters, numbers, hyphens and underscores.";
            if (v.Length < min || v.Length > max) return $"{label} must be {min} to {max} characters.";
            return null;
        }

        public static string? Number(string raw, string label, decimal min, decimal max, int maxDecimals = 2)
        {
            var v = raw.Trim();
            if (v.Length == 0) return $"{label} is required.";
            if (!decimal.TryParse(v, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var n))
                return $"{label} must be a number (for example 10 or 12.5).";
            if (n < min || n > max) return $"{label} must be between {min:0.##} and {max:0.##}.";
            var dot = v.IndexOf('.');
            if (dot >= 0 && v.Length - dot - 1 > maxDecimals) return $"{label} can have at most {maxDecimals} decimal places.";
            return null;
        }

        public static string? Password(string raw, bool required)
        {
            if (raw.Length == 0) return required ? "Password is required." : null;
            if (raw.Length < 8) return "Password must be at least 8 characters.";
            if (!raw.Any(char.IsLetter) || !raw.Any(char.IsDigit)) return "Password must include at least one letter and one number.";
            return null;
        }
    }

    // Watches form controls, shows a red icon on the control and the first error
    // in a red line on the form. ValidateAll() is called when the user clicks Save.
    internal sealed class FormValidator
    {
        private readonly ErrorProvider _provider = new() { BlinkStyle = ErrorBlinkStyle.NeverBlink };
        private readonly Dictionary<Control, Func<string?>> _rules = new();
        private readonly Dictionary<Control, string> _errors = new();
        private readonly List<Control> _order = new();
        private readonly Label _summary;

        public FormValidator(Label summaryLabel) => _summary = summaryLabel;

        public void Watch(Control control, Func<string?> rule)
        {
            _rules[control] = rule;
            _order.Add(control);

            switch (control)
            {
                case TextBox tb:
                    // Do not complain about an empty box while the user is still typing
                    tb.TextChanged += (s, e) => { if (tb.Text.Length > 0 || _errors.ContainsKey(tb)) Recheck(tb); };
                    tb.Leave += (s, e) => Recheck(tb);
                    break;
                case ComboBox cb:
                    cb.SelectedIndexChanged += (s, e) => Recheck(cb);
                    break;
                case DateTimePicker dtp:
                    dtp.ValueChanged += (s, e) => Recheck(dtp);
                    break;
            }
        }

        public void Recheck(Control control)
        {
            if (!_rules.TryGetValue(control, out var rule)) return;

            var message = rule();
            if (message is null) _errors.Remove(control);
            else _errors[control] = message;

            _provider.SetError(control, message ?? "");
            RefreshSummary();
        }

        public bool ValidateAll(bool showDialog = true)
        {
            foreach (var c in _order) Recheck(c);
            if (_errors.Count == 0) return true;

            if (showDialog)
            {
                var lines = _order.Where(c => _errors.ContainsKey(c)).Select(c => "•  " + _errors[c]);
                MessageBox.Show("Please fix the following before saving:\n\n" + string.Join("\n", lines),
                    "Invalid input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _order.First(c => _errors.ContainsKey(c)).Focus();
            }
            return false;
        }

        public void Reset()
        {
            foreach (var c in _order) _provider.SetError(c, "");
            _errors.Clear();
            RefreshSummary();
        }

        private void RefreshSummary()
        {
            var first = _order.FirstOrDefault(c => _errors.ContainsKey(c));
            _summary.Text = first is null ? "" : "⚠  " + _errors[first];
        }
    }
}