using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Morfologia;

internal class Filters
{
    
    public static void binarizar_semDMA(Bitmap imagem, int limiar = 128)
    {
        int width = imagem.Width;
        int height = imagem.Height;
    
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color pixel = imagem.GetPixel(x, y);
                
                int mediaCor = (pixel.R + pixel.G + pixel.B) / 3;
    
                if (mediaCor < limiar)
                {
                    imagem.SetPixel(x, y, Color.Black);
                }
                else
                {
                    imagem.SetPixel(x, y, Color.White);
                }
            }
        }
    }
    
    public static void binarizar_DMA(Bitmap imagem, int limiar = 128)
    {
        int width = imagem.Width;
        int height = imagem.Height;
        int pixelSize = 3;
    
        BitmapData bitmapData = imagem.LockBits(new Rectangle(0, 0, width, height),
            ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
    
        unsafe
        {
            byte* ptr = (byte*)bitmapData.Scan0.ToPointer();
            
            int stride = bitmapData.Stride;
            int padding = stride - (width * pixelSize);
    
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // calcula a média dos canais B, G e R do pixel atual
                    int mediaCor = (ptr[0] + ptr[1] + ptr[2]) / 3;
    
                    // Aplica o Limiar (Threshold)
                    if (mediaCor < limiar)
                    {
                        // Fica perfeitamente Preto
                        ptr[0] = 0;
                        ptr[1] = 0;
                        ptr[2] = 0;
                    }
                    else
                    {
                        // Fica perfeitamente Branco
                        ptr[0] = 255;
                        ptr[1] = 255;
                        ptr[2] = 255;
                    }
    
                    // Avança para o próximo pixel
                    ptr += pixelSize;
                }
                
                // Pula os bytes vazios do fim da linha
                ptr += padding;
            }
        }
    
        imagem.UnlockBits(bitmapData);
    }

    public static void dilatacao_DMA(Bitmap imgSrc, Bitmap imgDst)
    {
        
        binarizar_DMA(imgSrc, 128);
        
        int width = imgSrc.Width;
        int height = imgSrc.Height;
        int pixelSize = 3;
        
        // lock dados bitmap origem 
        BitmapData bitmapDataSrc = imgSrc.LockBits(new Rectangle(0, 0, width, height),
            ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
        // lock dados bitmap destino
        BitmapData bitmapDataDst = imgDst.LockBits(new Rectangle(0, 0, width, height),
            ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);

        unsafe
        {
            byte* src = (byte*)bitmapDataSrc.Scan0.ToPointer();
            byte* dst = (byte*)bitmapDataDst.Scan0.ToPointer();

            int stride = bitmapDataSrc.Stride;
            int padding = stride - (width * pixelSize);
        
            int origX = 1;
            int origY = 1;
            
            int b, x, y, i, j; // byte (tanto faz ser b, g ou r, pois a imagem é binária)
            
            // essa mascara funciona bem na primeira imagem (objeto preto, engordar letras)
            int[,] mascara = new int[3, 3]; // elemento estruturante 3x3
            for (y = 0; y < 3; y++) // criando o elemento estruturante 3x3, com origem no centro
            {
                for (x = 0; x < 3; x++)
                {
                    if ((y == 1 && x == 1) || 
                        (y == 1 && x == 0) || (y == 1 && x == 2) || 
                        (y == 2 && x == 1) || (y == 0 && x == 1))
                        // centro é origem, apenas os cantos não são pretos
                        mascara[x, y] = 0;
                    else 
                        mascara[x, y] = 255;
                }
            }
            
            // limpando a imagem de destino deixando tudo branco (fundo)
            for (y = 0; y < height; y++)
            {
                for (x = 0; x < width; x++)
                {
                    byte* pClear = dst + (y * bitmapDataDst.Stride) + (x * pixelSize);
                    pClear[0] = 255;
                    pClear[1] = 255;
                    pClear[2] = 255;
                }
            }

            // posicionando os ponteiros iniciais na coordenada (1, 1) para ignorar as bordas
            byte* pSrc = src + stride + pixelSize;
            byte* pDst = dst + stride + pixelSize;

            for (y = 1; y < height - 1; y++)
            {
                for (x = 1; x < width - 1; x++)
                {
                    // essa variavel vai dizer se a origem bate com algum ponto da imagem de origem
                    bool encontrou = false;
                    
                    // comparar pixel da imagem com a origem do elemento estruturante (Corrigido para Y, X)
                    if (pSrc[0] == mascara[origX, origY]) encontrou = true;

                    if (encontrou) // se encontrou o pixel preto na origem, aplicar a dilatação
                    {
                        for (i = 0; i < 3; i++)
                        {
                            for (j = 0; j < 3; j++)
                            {
                                if (mascara[i, j] == 0) // se a posição na matriz for preta
                                {
                                    // calcula a posição do pixel vizinho na imagem de destino e pinta de preto
                                    byte* pVizinho = pDst + ((j - origY) * stride) + ((i - origX) * pixelSize);
                                    pVizinho[0] = 0;
                                    pVizinho[1] = 0;
                                    pVizinho[2] = 0;
                                }
                            }
                        }
                    }
                    
                    // anda 1 pixel para a direita (x)
                    pSrc += pixelSize;
                    pDst += pixelSize;
                }
                
                // como é fim de linha, pula x=width-1, x=0 e o padding
                pSrc += (pixelSize * 2) + padding;
                pDst += (pixelSize * 2) + padding;
            }
        }
        // unlock imagem origem 
        imgSrc.UnlockBits(bitmapDataSrc);
        // unlock imagem destino
        imgDst.UnlockBits(bitmapDataDst);
    }
    
    public static void dilatacao_semDMA(Bitmap imgSrc, Bitmap imgDst)
    {
        
        binarizar_semDMA(imgSrc, 128);
        
        int width = imgSrc.Width;
        int height = imgSrc.Height;

        int i, j, x, y;
        int origX = 1;
        int origY = 1;

        // essa mascara funciona bem na primeira imagem (objeto preto, engordar letras)
        // cria o elemento estruturante 3x3, com origem no centro
        int[,] mascara = new int[3, 3];
        for (y = 0; y < 3; y++)
        {
            for (x = 0; x < 3; x++)
            {
                if ((y == 1 && x == 1) || 
                    (y == 1 && x == 0) || (y == 1 && x == 2) || 
                    (y == 2 && x == 1) || (y == 0 && x == 1))
                    // centro é origem, apenas os cantos não são pretos
                    mascara[x, y] = 0;
                else 
                    mascara[x, y] = 255;
            }
        }
        
        // pinta a imagem de destino de branco (fundo)
        for (y = 0; y < height; y++)
        {
            for (x = 0; x < width; x++)
            {
                imgDst.SetPixel(x, y, Color.White);
            }
        }

        // percorre a imagem de origem INTEIRA (sem ignorar as bordas)
        for (y = 0; y < height; y++)
        {
            for (x = 0; x < width; x++)
            {
                // pega APENAS o pixel central na imagem de origem
                Color pixelColor = imgSrc.GetPixel(x, y);
                
                // se o pixel da imagem de origem for igual a origem do elemento estruturante (Corrigido Y, X)
                if(pixelColor.R == mascara[origX, origY]) 
                {
                    // aplicar a dilatação
                    for (i = 0; i < 3; i++)
                    {
                        for (j = 0; j < 3; j++)
                        {
                            if (mascara[i, j] == 0) // se a posição na matriz for preta
                            {
                                // calcula a coordenada do vizinho descontando a origem
                                int vizinhoX = x + i - origX;
                                int vizinhoY = y + j - origY;

                                // protege as bordas verificando se o vizinho está dentro da imagem
                                if (vizinhoX >= 0 && vizinhoX < width && vizinhoY >= 0 && vizinhoY < height)
                                {
                                    imgDst.SetPixel(vizinhoX, vizinhoY, Color.Black);
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    public static void erosao_DMA(Bitmap imgSrc, Bitmap imgDst)
    {
            
        binarizar_DMA(imgSrc, 128);
        
        int height = imgSrc.Height;
        int width = imgSrc.Width;
        int pixelSize = 3;
        
        
        //lock dados bitmap origem 
        BitmapData bitmapDataSrc = imgSrc.LockBits(new Rectangle(0, 0, width, height),
            ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
        //lock dados bitmap destino
        BitmapData bitmapDataDst = imgDst.LockBits(new Rectangle(0, 0, width, height),
            ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);

        unsafe
        {

            int x, y, i, j;
            int origX = 1;
            int origY = 1;
            
            int[,] mascara = new int[3, 3]; // elemento estruturante 3x3
            for (y = 0; y < 3; y++) // criando o elemento
            {
                for (x = 0; x < 3; x++)
                {
                    if ((y == origY && x == origX) || (y == 1 && x == 0) || (y == 1 && x == 2) || 
                        (y == 2 && x == 1) || (y == 0 && x == 1))
                        mascara[x, y] = 0;
                    else mascara[x, y] = 255;
                }
            }

            byte* src = (byte*)bitmapDataSrc.Scan0.ToPointer();
            byte* dst = (byte*)bitmapDataDst.Scan0.ToPointer();
            
            int stride = bitmapDataSrc.Stride;
            int padding = stride - (width * pixelSize);
            
            // pintando a imagem de destino de branco (fundo)
            byte* pClear = dst;
            for (y = 0; y < height; y++)
            {
                for (x = 0; x < width; x++)
                {
                    pClear[0] = 255; pClear[1] = 255; pClear[2] = 255;
                    pClear += pixelSize;
                }
                pClear += padding;
            }
            
            // posicionando os ponteiros iniciais na coordenada (1, 1) para ignorar as bordas
            byte* pSrc = src + stride + pixelSize;
            byte* pDst = dst + stride + pixelSize;
            
            for(y = 1; y < height - 1; y++)
            {
                for(x = 1; x < width - 1; x++)
                {
                    
                    // começar a comparar o elemento estruturante com a imagem de origem
                    
                    // essa variavel vai dizer se todos os pixel da mascara bateram com aquele pedaço da imagem de origem
                    bool encaixa = true;
                    
                    // comparar os pixels da imagem com a mascara do elemento estruturante
                    for (i = 0; i < 3 && encaixa; i++)
                    {
                        for (j = 0; j < 3 && encaixa; j++)
                        {
                            // pega o vizinho relativo ao pSrc atual
                            byte* pVizinhoSrc = pSrc + ((j - origY) * stride) + ((i - origX) * pixelSize);
        
                            // se a máscara for completamente igual a imagem de origem (preto igual objeto), então encaixa continua true
                            // assim, os brancos da mascara não precisam ser testados, apenas os pretos (devido o objeto ser preto)
                            // acho que poderia ser o contrario tbm, depende do objeto ser preto ou branco
                            if (mascara[i, j] == 0) // só testa onde o elemento estruturante é preto
                            {
                                if (pVizinhoSrc[0] != 0) // se imagem também é preta
                                {
                                    encaixa = false;
                                }
                            }
                        }
                    }

                    if (encaixa) 
                    {
                        // pega o valor exato do pixel de origem da máscara (tipo 0 ou 255) e coloca no destino
                        byte cor = (byte)mascara[origX, origY];
    
                        pDst[0] = cor;
                        pDst[1] = cor;
                        pDst[2] = cor;
                    }
                    
                    // anda 1 pixel para a direita (x)
                    pSrc += pixelSize;
                    pDst += pixelSize;
                    
                }
                
                // como é fim de linha, ent pula a borda direita (x=width-1), a borda esquerda da próxima (x=0) e o padding
                pSrc += (pixelSize * 2) + padding;
                pDst += (pixelSize * 2) + padding;
            }

        }
        //unlock imagem origem 
        imgSrc.UnlockBits(bitmapDataSrc);
        //unlock imagem destino
        imgDst.UnlockBits(bitmapDataDst);

    }

    public static void erosao_semDMA(Bitmap imgSrc, Bitmap imgDst)
    {
        
        binarizar_semDMA(imgSrc, 128);
        
        int width = imgSrc.Width;
        int height = imgSrc.Height;

        int i, j, x, y;
        int origX = 1;
        int origY = 1;

        // cria o elemento estruturante 3x3
        int[,] mascara = new int[3, 3];
        for (y = 0; y < 3; y++)
        {
            for (x = 0; x < 3; x++)
            {
                if ((y == origY && x == origX) || (y == 1 && x == 0) || (y == 1 && x == 2) ||
                    (y == 2 && x == 1) || (y == 0 && x == 1))
                    mascara[x, y] = 0;
                else mascara[x, y] = 255;
            }
        }

        // pinta a imagem de destino de branco (fundo)
        for (y = 0; y < height; y++)
        {
            for (x = 0; x < width; x++)
            {
                imgDst.SetPixel(x, y, Color.White);
            }
        }

        // percorre a imagem de origem ignorando as bordas
        for (y = 1; y < height - 1; y++)
        {
            for (x = 1; x < width - 1; x++)
            {
                bool encaixa = true;

                // comparar os pixels da imagem com a mascara do elemento estruturante
                for (i = 0; i < 3 && encaixa; i++)
                {
                    for (j = 0; j < 3 && encaixa; j++)
                    {
                        // pega o vizinho relativo ao pixel atual
                        Color vizinhoSrc = imgSrc.GetPixel(x + i - origX, y + j - origY);

                        // se a máscara for diferente da imagem de origem (só olha os pretos, por causa do objeto), encaixa = false
                        if (mascara[i, j] == 0) // testa apenas onde o elemento estruturante é preto
                        {
                            if (vizinhoSrc.R != 0) // se imagem também é preta
                            {
                                encaixa = false;
                            }
                        }
                    }
                }
            
                if (encaixa)
                {
                    // pega o valor do pixel de origem da máscara e coloca no destino
                    byte cor = (byte)mascara[origX, origY];
                    imgDst.SetPixel(x, y, Color.FromArgb(cor, cor, cor));
                }
            }
        }
    }

    // ABERTURA é ED (Erosão + Dilatação)
    // FECHAMENTO é DE (Dilatação + Erosão)
    
    public static void abertura_semDMA(Bitmap imgSrc, Bitmap imgDst)
    {
        //
    }
    
    public static void fechamento_semDMA(Bitmap imgSrc, Bitmap imgDst)
    {
        //
    }
    
    public static void abertura_DMA(Bitmap imgSrc, Bitmap imgDst)
    {
        //
    }
    
    public static void fechamento_DMA(Bitmap imgSrc, Bitmap imgDst)
    {
        //
    }
    
}