using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading;
using System.Numerics;

internal class BlockParams {
    public Complex Origin;
    public int PixelOffsetX;
    public int PixelOffsetY;
}

abstract public class ComplexFunctionWindow : Window {

    WriteableBitmap bmp;
    Complex center = new Complex( 0.0, 0.0 );
    double scale = 4.0;
    readonly int BLOCK_SIZE = 100;

    public int Pixels {
        get { return Convert.ToInt32(SystemParameters.PrimaryScreenHeight) - 100; }
    }

    public ComplexFunctionWindow( ) {
        this.Left = 20.0;
        this.Top = 20.0;
        SizeToContent = SizeToContent.WidthAndHeight;
        var image = new Image( );
        image.Width = Pixels;
        image.Height = Pixels;
        this.Content = image;
        bmp = new WriteableBitmap( Pixels, Pixels, 96, 96, PixelFormats.Bgra32, null );
        image.Source = bmp;
        Draw( );
    }

    private double Step { get { return scale / Pixels; } }

    private double BlockRealSize { get { return BLOCK_SIZE * Step; } }

    private void Draw( ) {
        var origin = new Complex( center.Real - scale * 0.5, center.Imaginary - scale * 0.5 );
        var block_count = Pixels / BLOCK_SIZE;
        for ( int y_block = 0; y_block < block_count; ++y_block ) {
            for ( int x_block = 0; x_block < block_count; ++x_block ) {
                var blockOrigin = new Complex( origin.Real + x_block * BlockRealSize, origin.Imaginary + y_block * BlockRealSize );
                var blockParams = new BlockParams { Origin = blockOrigin, PixelOffsetX = x_block * BLOCK_SIZE, PixelOffsetY = y_block * BLOCK_SIZE };
                ThreadPool.QueueUserWorkItem( new WaitCallback( DrawBlockOuter ), blockParams );
            }
        }
    }

    private void DrawBlockOuter( object state ) {
        DrawBlock( (BlockParams)state );
    }

    private void DrawBlock( BlockParams par ) {
        byte[] buffer = new byte[BLOCK_SIZE * BLOCK_SIZE * 4];
        for ( int y = 0; y < BLOCK_SIZE; ++y ) {
            for ( int x = 0; x < BLOCK_SIZE; ++x ) {
                var pixel_x = x + par.PixelOffsetX;
                var pixel_y = y + par.PixelOffsetY;
                if ( pixel_x >= Pixels || pixel_y >= Pixels ) continue;
                var c = par.Origin + new Complex( x * Step, y * Step );
                var color = FunctionColor(c);
                var bufferOffset = ( y * BLOCK_SIZE + x ) * 4;
                buffer[bufferOffset] = color.B;
                buffer[bufferOffset + 1] = color.G;
                buffer[bufferOffset + 2] = color.R;
                buffer[bufferOffset + 3] = color.A;
            }
        }
        Dispatcher.BeginInvoke( new Action( ( ) => {
            bmp.WritePixels( new Int32Rect( 0, 0, BLOCK_SIZE, BLOCK_SIZE ), buffer, 4 * BLOCK_SIZE, par.PixelOffsetX, par.PixelOffsetY );
        } ), DispatcherPriority.Background );
    }

    protected abstract Color FunctionColor( Complex value );

}
