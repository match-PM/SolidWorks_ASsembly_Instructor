using System;
using System.Globalization;
using System.Windows.Forms;

namespace SolidWorks_ASsembly_Instructor
{
    // No grouping separators: both 10.00 and 10,00 mean ten in every locale.
    internal sealed class ConstraintNumberBox : NumericUpDown
    {
        public bool TryCommit()
        {
            // Display formatting may round Value. Only parse text the user edited;
            // applying an unchanged field must preserve its full stored precision.
            if (!UserEdit) return true;
            decimal value;
            if (!decimal.TryParse(Text.Trim().Replace(',', '.'),
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out value)
                || value < Minimum || value > Maximum)
                return false;

            UserEdit = false;
            Value = value;
            UpdateEditText();
            return true;
        }

        protected override void ValidateEditText()
        {
            // Keep invalid text visible so Apply can report it. The base parser
            // permits thousands separators and must never see uncommitted text.
            TryCommit();
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            if (e.KeyChar == '.' || e.KeyChar == ',')
                e.KeyChar = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator[0];
            base.OnKeyPress(e);
        }

        public override void UpButton()
        {
            if (TryCommit()) base.UpButton();
        }

        public override void DownButton()
        {
            if (TryCommit()) base.DownButton();
        }
    }
}
