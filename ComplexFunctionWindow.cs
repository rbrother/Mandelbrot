using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading;
using System.Threading.Tasks;
using System.Numerics;

internal class BlockParams {
    public Complex Origin;
    public int PixelOffsetX;
    public int PixelOffsetY;
}

abstract public class ComplexFunctionWindow : Window {

    Image image;
    WriteableBitmap Bmp;
    Complex Center { get; set; }
    double RealSize = 4.0;
    readonly int BLOCK_SIZE = 100;
    private CancellationTokenSource _cancellationSource = null;
    private CancellationToken _cancellationToken;

    public int Pixels {
        get { return Convert.ToInt32(SystemParameters.PrimaryScreenHeight) - 100; }
    }

    public ComplexFunctionWindow( ) {
        this.Left = 20.0;
        this.Top = 20.0;
        Center = new Complex( 0.0, 0.0 );
        SizeToContent = SizeToContent.WidthAndHeight;
        image = new Image( );
        image.Width = Pixels;
        image.Height = Pixels;
        this.Content = image;
        Bmp = new WriteableBitmap( Pixels, Pixels, 96, 96, PixelFormats.Bgra32, null );
        image.Source = Bmp;
        image.MouseWheel += image_MouseWheel;
        image.MouseUp += image_MouseUp;
        Draw( );
    }

    private Complex Origin { get { return new Complex( Center.Real - RealSize * 0.5, Center.Imaginary - RealSize * 0.5 ); } }

    private double Step { get { return RealSize / Pixels; } }

    private double BlockRealSize { get { return BLOCK_SIZE * Step; } }

    void image_MouseUp( object sender, System.Windows.Input.MouseButtonEventArgs e ) {
        var pos = e.MouseDevice.GetPosition( image );
        Center = Origin + new Complex( pos.X, pos.Y ) * Step;
        Draw( );
    }

    void image_MouseWheel( object sender, System.Windows.Input.MouseWheelEventArgs e ) {
        var Scale = 1.0 / (1.0 + 0.002 * e.Delta);
        var invScale = 1.0 / Scale;
        var transformed = new TransformedBitmap( Bmp, new ScaleTransform( invScale, invScale ) );
        var buffer = new byte[Pixels * Pixels * 4];
        if ( e.Delta > 0 ) {
            var offset = Convert.ToInt32( Pixels * ( invScale - 1 ) * 0.5 );            
            transformed.CopyPixels( new Int32Rect( offset, offset, Pixels, Pixels ), buffer, Pixels * 4, 0 );
            Bmp.WritePixels( new Int32Rect( 0, 0, Pixels, Pixels ), buffer, Pixels * 4, 0 );
        } else {
            var offset = Convert.ToInt32( Pixels * ( 1 - invScale ) * 0.5 );
            transformed.CopyPixels( new Int32Rect( 0, 0, transformed.PixelWidth, transformed.PixelHeight ), buffer, Pixels * 4, 0 );
            Bmp.WritePixels( new Int32Rect( 0, 0, Pixels, Pixels ), new byte[Pixels * Pixels * 4], Pixels * 4, 0 ); // Clear
            Bmp.WritePixels( new Int32Rect( offset, offset, transformed.PixelWidth, transformed.PixelWidth ), buffer, Pixels * 4, 0 );
        }
        RealSize = RealSize * Scale;
        this.Title = string.Format( "Scale {0}", RealSize );
        Draw( );
    }

    private void Draw( ) {
        if ( _cancellationSource != null ) _cancellationSource.Cancel( );
        _cancellationSource = new CancellationTokenSource( );
        _cancellationToken = _cancellationSource.Token;
        var block_count = Pixels / BLOCK_SIZE;
        for ( int y_block = 0; y_block < block_count; ++y_block ) {
            for ( int x_block = 0; x_block < block_count; ++x_block ) {
                var blockOrigin = Origin + new Complex( x_block, y_block ) * BlockRealSize;
                var blockParams = new BlockParams { Origin = blockOrigin, PixelOffsetX = x_block * BLOCK_SIZE, PixelOffsetY = y_block * BLOCK_SIZE };
                Task.Factory.StartNew( new Action( ( ) => DrawBlock( blockParams ) ), _cancellationToken );
            }
        }
    }

    private void DrawBlock( BlockParams par ) {
        if ( _cancellationToken.IsCancellationRequested ) return;
        byte[] buffer = new byte[BLOCK_SIZE * BLOCK_SIZE * 4];
        for ( int y = 0; y < BLOCK_SIZE; ++y ) {
            for ( int x = 0; x < BLOCK_SIZE; ++x ) {
                if ( _cancellationToken.IsCancellationRequested ) return;
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
            Bmp.WritePixels( new Int32Rect( 0, 0, BLOCK_SIZE, BLOCK_SIZE ), buffer, 4 * BLOCK_SIZE, par.PixelOffsetX, par.PixelOffsetY );
        } ), DispatcherPriority.Background );
    }

    protected abstract Color FunctionColor( Complex value );

}
