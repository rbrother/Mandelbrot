using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Numerics;

public static class ComplexExtensions {

    public static double MagnitudeSqr( this Complex c ) {
        return c.Real * c.Real + c.Imaginary * c.Imaginary;
    }
}

internal class BlockParams {
    public Complex Origin;
    public int PixelOffsetX;
    public int PixelOffsetY;
}

abstract public class ComplexFunctionWindow : Window {

    Image image;
    WriteableBitmap Bmp;
    Complex Center { get; set; }
    double RealHeight = 4.0;
    readonly int BlockPixelSize = 100;
    private CancellationTokenSource _cancellationSource = null;
    private CancellationToken _cancellationToken;
    
    private int totalBlocks = 0;
    private int acceleratedBlocks = 0;

    double RealWidth { get { return RealHeight * PixelWidth / PixelHeight; } }

    public int PixelWidth { get { return Convert.ToInt32( SystemParameters.PrimaryScreenWidth ); } }
    public int PixelHeight { get { return Convert.ToInt32( SystemParameters.PrimaryScreenHeight ); } }

    public ComplexFunctionWindow( ) {
        this.WindowState = System.Windows.WindowState.Maximized;
        this.WindowStyle = System.Windows.WindowStyle.None;
        Center = new Complex( 0.0, 0.0 );
        image = new Image( );
        this.Content = image;
        image.MouseWheel += image_MouseWheel;
        image.MouseLeftButtonUp += image_MouseUp;
        image.MouseRightButtonUp += image_MouseRightButtonUp;
        Bmp = new WriteableBitmap( PixelWidth, PixelHeight, 96, 96, PixelFormats.Bgra32, null );
        image.Source = Bmp;
        Draw( );
    }

    private Complex Origin { get { return new Complex( Center.Real - RealWidth * 0.5, Center.Imaginary - RealHeight * 0.5 ); } }

    private double PixelStep { get { return RealHeight / PixelHeight; } }

    private double BlockRealSize { get { return BlockPixelSize * PixelStep; } }

    void image_MouseUp( object sender, System.Windows.Input.MouseButtonEventArgs e ) {
        var pos = e.MouseDevice.GetPosition( image );
        Center = Origin + new Complex( pos.X, pos.Y ) * PixelStep;
        Draw( );
    }

    void image_MouseRightButtonUp( object sender, System.Windows.Input.MouseButtonEventArgs e ) {
        Zoom( zoomIn: true, Scale: 1.5 );
    }

    void image_MouseWheel( object sender, System.Windows.Input.MouseWheelEventArgs e ) {
        var zoomIn = e.Delta > 0;
        var Scale = 1.0 + 0.002 * Math.Abs( e.Delta );
        if ( e.Delta < 0 ) Scale = 1.0 / Scale;
        Zoom( zoomIn, Scale );
    }

    private void Zoom( bool zoomIn, double Scale ) {
        var InverseScale = 1.0 / Scale;
        var transformed = new TransformedBitmap( Bmp, new ScaleTransform( Scale, Scale ) );
        var buffer = new byte[PixelWidth * PixelHeight * 4];
        if ( zoomIn ) {
            var offsetX = Convert.ToInt32( PixelWidth * ( Scale - 1 ) * 0.5 );
            var offsetY = Convert.ToInt32( PixelHeight * ( Scale - 1 ) * 0.5 );
            transformed.CopyPixels( new Int32Rect( offsetX, offsetY, PixelWidth, PixelHeight ), buffer, PixelWidth * 4, 0 );
            Bmp.WritePixels( new Int32Rect( 0, 0, PixelWidth, PixelHeight ), buffer, PixelWidth * 4, 0 );
        } else {
            var offsetX = Convert.ToInt32( PixelWidth * ( 1 - Scale ) * 0.5 );
            var offsetY = Convert.ToInt32( PixelHeight * ( 1 - Scale ) * 0.5 );
            transformed.CopyPixels( new Int32Rect( 0, 0, (int)transformed.Width, (int)transformed.Height ), buffer, PixelWidth * 4, 0 );
            Bmp.WritePixels( new Int32Rect( 0, 0, PixelWidth, PixelHeight ), new byte[PixelWidth * PixelHeight * 4], PixelWidth * 4, 0 ); // Clear
            Bmp.WritePixels( new Int32Rect( offsetX, offsetY, (int)transformed.Width, (int)transformed.Height ), buffer, PixelWidth * 4, 0 );
        }
        RealHeight = RealHeight * InverseScale;
        Draw( );
    }

    private void Draw( ) {
        var start = DateTime.Now;
        int BlockCountX = PixelWidth / BlockPixelSize;
        int BlockCountY = PixelHeight / BlockPixelSize;
        if ( _cancellationSource != null ) _cancellationSource.Cancel( );
        _cancellationSource = new CancellationTokenSource( );
        _cancellationToken = _cancellationSource.Token;
        var tasks = new List<Task>( );
        for ( int y_block = 0; y_block < BlockCountY; ++y_block ) {
            for ( int x_block = 0; x_block < BlockCountX; ++x_block ) {
                var blockOrigin = Origin + new Complex( x_block, y_block ) * BlockRealSize;
                var blockParams = new BlockParams { Origin = blockOrigin, PixelOffsetX = x_block * BlockPixelSize, PixelOffsetY = y_block * BlockPixelSize };
                tasks.Add( Task.Factory.StartNew( new Action( ( ) => DrawBlock( blockParams ) ), _cancellationToken ) );
            }
        }
        Task.Factory.ContinueWhenAll( tasks.ToArray( ), new Action<Task[]>( t => {
            Dispatcher.BeginInvoke( new Action( ( ) => {
                var duration = DateTime.Now - start;
                Debug.Print("Finished: Scale {0}, duration {1:0.0} sec, Normal/accelerated blocks {2}/{3}", 
                    RealHeight, duration.TotalSeconds, acceleratedBlocks, totalBlocks ); 
                acceleratedBlocks = 0;
                totalBlocks = 0;
            } ) ); 
        } ) );
    }

    private bool EdgesHaveColor( BlockParams par, Color color ) {
        for ( int n = 0; n < BlockPixelSize; ++n ) {
            var points = new Complex[] { new Complex( 0, n ), new Complex( BlockPixelSize, n ), new Complex( n, 0 ), new Complex( n, BlockPixelSize ) };
            foreach ( var point in points ) {
                if ( FunctionColor( par.Origin + point * PixelStep ) != color ) return false;
            }
        }
        acceleratedBlocks++;
        return true;
    }

    private void DrawBlock( BlockParams par ) {
        if ( _cancellationToken.IsCancellationRequested ) return;
        totalBlocks++;
        // Quick method: if edges of the block are same color, then internal must be also (speeds up black areas)
        var topLeftColor = FunctionColor( par.Origin );
        Color? singleColor = EdgesHaveColor( par, topLeftColor ) ? (Color?) topLeftColor : null;
        byte[] buffer = new byte[BlockPixelSize * BlockPixelSize * 4];
        for ( int y = 0; y < BlockPixelSize; ++y ) {
            for ( int x = 0; x < BlockPixelSize; ++x ) {
                if ( _cancellationToken.IsCancellationRequested ) return;
                var color = singleColor.HasValue ? singleColor.Value : FunctionColor( par.Origin + new Complex( x, y ) * PixelStep );
                var bufferOffset = ( y * BlockPixelSize + x ) * 4;
                buffer[bufferOffset] = color.B;
                buffer[bufferOffset + 1] = color.G;
                buffer[bufferOffset + 2] = color.R;
                buffer[bufferOffset + 3] = color.A;
            }
        }
        Dispatcher.BeginInvoke( new Action( ( ) => {
            if ( par.PixelOffsetX + BlockPixelSize <= Bmp.Width && par.PixelOffsetY + BlockPixelSize <= Bmp.Height ) {
                Bmp.WritePixels( new Int32Rect( 0, 0, BlockPixelSize, BlockPixelSize ), buffer, 4 * BlockPixelSize, par.PixelOffsetX, par.PixelOffsetY );
            }
        } ), DispatcherPriority.Background );
    }

    virtual protected Color ColorMap( int i ) {
        return Color.FromRgb(
            255,
            Convert.ToByte( Math.Abs( 255 - i * 32 % 512 ) ),
            Convert.ToByte( Math.Abs( 255 - i * 8 % 512 ) ) );
    }

    protected abstract Color FunctionColor( Complex value );

}
