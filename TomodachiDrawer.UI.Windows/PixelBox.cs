namespace TomodachiDrawer.UI.Windows
{
    public class PixelBox : PictureBox
    {
        protected override void OnPaint(PaintEventArgs pe)
        {
            pe.Graphics.InterpolationMode = System
                .Drawing
                .Drawing2D
                .InterpolationMode
                .NearestNeighbor;
            pe.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            base.OnPaint(pe);
        }
    }
}
