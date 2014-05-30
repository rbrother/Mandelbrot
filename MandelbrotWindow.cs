using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading;
using System.Threading.Tasks;
using System.Numerics;

namespace Mandelbrot {

    internal class BlockParams {
        public Complex Origin;
        public int PixelOffsetX;
        public int PixelOffsetY;
    }

    public partial class MandelbrotWindow : ComplexFunctionWindow {

        WriteableBitmap bmp;
        Complex center = new Complex( 0.0, 0.0 );
        double scale = 4.0;
        int pixels = 1200;
        readonly int BLOCK_SIZE = 50;

        public MandelbrotWindow( ) {
            this.Title = "Mandelbrot Fractal";
            SizeToContent = SizeToContent.WidthAndHeight;
            var image = new Image( );
            image.Width = 1200;
            image.Height = 1200;
            this.Content = image;
            bmp = new WriteableBitmap( pixels, pixels, 96, 96, PixelFormats.Bgra32, null );
            image.Source = bmp;
            Draw( );
        }

        private readonly byte[] BLACK = new byte[] { 0, 0, 0, 255 };

        private double Step { get { return scale / pixels; } }

        private double BlockRealSize { get { return BLOCK_SIZE * Step; } }

        private void Draw( ) {
            var origin = new Complex( center.Real - scale * 0.5, center.Imaginary - scale * 0.5 );
            var block_count = pixels / BLOCK_SIZE;
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
                    if ( pixel_x >= pixels || pixel_y >= pixels ) continue;
                    var c = par.Origin + new Complex( x * Step, y * Step );
                    var iterations = Iterations( c );
                    var color = iterations.HasValue ? ColorMap( iterations.Value ) : BLACK;
                    var bufferOffset = ( y * BLOCK_SIZE + x ) * 4;
                    buffer[bufferOffset] = color[0];
                    buffer[bufferOffset + 1] = color[1];
                    buffer[bufferOffset + 2] = color[2];
                    buffer[bufferOffset + 3] = color[3];
                }
            }
            Dispatcher.BeginInvoke( new Action( ( ) => {
                bmp.WritePixels( new Int32Rect( 0, 0, BLOCK_SIZE, BLOCK_SIZE ), buffer, 4 * BLOCK_SIZE, par.PixelOffsetX, par.PixelOffsetY );
            } ), DispatcherPriority.Background );
        }

        private static int? Iterations( Complex c ) {
            var z = new Complex( 0.0, 0.0 );
            int iteration = 0;
            int max_iteration = 256;
            while ( iteration < max_iteration ) {
                z = z * z + c;
                if ( z.Magnitude > 2 ) return iteration;
                iteration++;
            }
            return null;
        }

        private static byte[] ColorMap( int i ) {
            return new byte[] { 
                Convert.ToByte( Math.Abs( 255 - i * 32 % 512 ) ), 
                Convert.ToByte( Math.Abs( 255 - i * 16 % 512 ) ), 
                255, 
                255 };
        }

    } // class

} // namespace
