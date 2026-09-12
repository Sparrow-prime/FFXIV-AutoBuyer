using System.Collections.Generic;
using System.IO;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Textures;
using Dalamud.Utility;
using Lumina.Text.ReadOnly;
using OmenTools.Info.Dalamud;

namespace DailyRoutines.Common.Info;

public static class Assets
{
    public static ISharedImmediateTexture Icon { get; } =
        ITextureProvider.Instance().GetFromFile(Path.Join(IDalamudPluginInterface.Instance().AssemblyLocation.DirectoryName, "Assets", "icon.png"));

    public static ISharedImmediateTexture BackgroundTexture { get; } =
        ITextureProvider.Instance().GetFromFile(Path.Join(IDalamudPluginInterface.Instance().AssemblyLocation.DirectoryName, "Assets", "background.png"));

    public static List<StyleInfo> Styles { get; } =
    [
        new
        (
            "Tropical Sanctuary",
            "DS1H4sIAAAAAAAACpWXXXPaOhCG/8oZXXsYWbblj7umnNNcNJ1Mw5m0vVOMYlSM5QpDknb47x3ZWlsGDMoVoHkfdvVqtV7/QQxl/gx76Allf9A3lPlE//qOMh/P8MFDOSwtUYb1JzdCPItaHZ5FBw89o8z3UAHilRELWGBPZuUnxGl/fUcZbcOsUea3eZRGtzG6ZCSrztPyrLgGMeyoXf2FMtLSCmU+1V+28H8NfNnBlz3KAv35Avt4Nem9nXXhd7+KrVXGzDIdpZfLUq8v+GvTY2louDTx0A+T+iOkrqVzsWVPJV/2CDVE3AJ4FicW8SiqpXy5KYa8ojAJEpzEhvIJDsOQAkxwSH2ss+j/4uNKlEv7H+I0JgmBsD4NfUID4GNKoxh3PJ6FBw/dy3pXj3ggQ4BAniYHD91IteTqxEhsxJ0fXW120ocVW8oXJ+A/xTbcysXHfkpxnMBmiGEC4kc0obYNBr2Ve64s931i0BB2QynsZqA+5I3Y81MohoApQIkumIVoSjtNDDXmG7lvn7FRH8WYtHkEfZRlyeqttaEr3B2vdjdM2cn1lQCbIZb+IVeyLJ/GBBQ57oq8i3WKfFK6axx7piuvhSaZyTOK4IrQKdT1oOBq8Hx9x9S6B8C8tPdiZEYplnxiW6elkEYjZJQatDQ8i8NzHt7smkZWJ2IKQXyIQXrx2LWBSci5ouuYiZxCOgrTEbecje510jcQsMq+OJ3a9STxLIp6yP2yUW0wr5lijRwyC4/N0g60et/WT+Z2GofYnOM5aj8OHvrKt+I3/6RE7WACISPiHRmGY/IdFmpwYRX05XawcL+d5kwXJ5dyqBxoH8E4wv/Vs8x3o6Z2uYFayPu66Fzma1EV94rvBX9x8SsYqH83dfPm3OXvS9l8FhXfujanHnjfxdbYrdg2slBs49BAomNmKtzZprDQs0x3bV1GBAsyz/5Gyapwubp0DH4Wxapx4HAC3NfxHHVhyhjkH8rGZe4hbRD+2jzwkucNt8etS21Fj1dzxYq5kvWCqYI3Vw+5665f2P5WFKvSMuESos/4C9t3w6SoimP2cpbJET0XGwcjA91Y7uSSlR3oRkXkoN8S2IajDC2UrEXOyn8eWJU3O6bekIeW3dTNTrZN+hnYbidDY9O1MT1o50Mt9S8E/URod8ChKRHoY0E/0USWkDu+FDz3un6MTc82hOLabe5kK9cGIxynnp+OVq9PK7EfDSxZOZxIcNxaUtvAjePRVY77gJfMq8YMj+orGf5yOxEYTSwD4VkZ2X8HjwbLQCjBYNAdPLTXcxg+/AVMRvRx/Q8AAA=="
        ),
        new
        (
            "Code Cobalt",
            "DS1H4sIAAAAAAAACqWYW3ObOBSA/0qG52yHi0DgtybeNg9tJxNnp5u+ybaCqYnlYuw0zeS/79EVSdg7AechNvicj3OXxGtAgkn0IbwM5sHkNfg3mOT84kF8vl0Gi2CS8RvLYBLyT6qkQiUVfkhB6hEYl0GpZFeKWAUTxD/JXMn/VMqZUoZfQXmtxGol9aSkkJJKhdTGuyt1mXc3Fne3R2V/wc/CrgbsEybslLct3BCP3iudgyBdBs/q+rcy7cV4n1re/+nFJOK3CVH3E3U/kRFl4OhrcE9/t0avQFGEM80s0izPYgyXP8SluEJw+V0EFhhceVrtyLymSwNBUZGFONcUhHOURAlWlBQVIQgkFuV7tVmy56vSEKJQIjLtBhiFcJgpRCQNySzE9aqqlzYhjrjxiQagNCzyUHsSW5q3bLvf2ppG9MF9ltTkAYDfLcAVa5a0MfqdhNCPM/GXK/3E0D392YpADLoECrGk0A6EIvKRooQmT4byqSFP1HZDR0xaASiIiM5BjMM0z+Mj+jfsQBsrl3GcpDE8TXNk9ow3Msqoz/m4aKtD16MynUh701ElRqTbjul91daON647kXAfaTM6ug/wzeCQNC80xuipqIjkxH3MNatrst1ZcRlM+ko3+yvS2D4lmIdThzaShFgjRGRjOyizRQN2zF2ILJNYQ0SCNMMErY/43PBhOLZkHUyvZER/a7cS+YcUDAknI3QK5iVMlon2zkBk+dqQ6xVdrL+SZn3CjFTkq1CAvDfIZnUFTehGZSTgf11w2rjfxVf7tmWbU4lBsm10ejMehCTt6/cyIgWRGcg6fAKD+00sMX7ziEKLdA8m3K0Im7Eu82wX2w0l9mAc3DJS/+yJJDFnDqQZ3ZKGtGzUnO+iY5PO7hsNGtszd3RX/aGfm2o7uOaB5wB8XwYVfYfxPPFCnOqsSHNkyuxhbTXv8El/ZJANbRtg+B7kSVhgbBAxwgk2GyoY73mWFpmL+GfzyBZ7Z7EZvPpZFN8is648OAn/4Q4Yg5qyxbralLcNPVT0eeRwVJC/n7bty1nbvNuatV+qDd2NqzSj7qcauVHJrKiInbpDuKl2LSthn9Opu1bk0hE9GfnOKytOQXqmyAXdsHQLqJEvl3c70zWVE+6crYXAqI1o27BNOX5vYKG+VOWqHb0jFaC70eeC0IN8rNsx8ZGTjp9zZrSmi5bap4z3j8uYd0FDymnDtvekKak2RpSXzLQ4JcXaGbvqvpHDDcSyduI5qPKBIE9Z0IY+6r0mGMC0ehqdlJzvhdmS1JLmogacesDjN34ah9NGMAmmpKpfLu7YvuXdffHXxTVbUvg3J5B1eGkgj7qk53B4xNNuEcFKShcptqTgfcQ7pJb9c7qeCpYU9ewSn73R82hYOdbtKL7JcuPfjGzZ8xSHujBt5qr3xiA78uSqJ2XWHrlLsWT1qxX+RLVcnoz0+oTfYtdipOqO6Ie6cGrcmsf+5seS6nbXUEzeY23j9BudzmmUyBC6wek2Txn/XQ03Y6ETHHjz46UF9jRHnt3tMmGK6KcbJuz7LdluKczNlNbpEXQlCaKw0MDdt/8ADUD9a2sTAAA="
        ),
        new
        (
            "Nebula Serene",
            "DS1H4sIAAAAAAAACqWYS3ObMBCA/0qGc9oRAgT41sRtc2g6nTidPm6yrWBqAi7GTtNM/3tXb/HoTAFfsIT20+5qdyXx4lFv4b9Gl97aW7x4X71FwhvfxPPPpbfxFoR3bL0F4k+mRiE1Cr2OYNQDMC69zFuEvHunXueqTdeq44cSJkoY3oLwXg0r1KhHNSpsjSoHe6tOLxa9B9WLW70/4Y+wtAZthQpHJdhAh5j6pDrO6vmknr+Uas/G+six/nfPJ3w6Sgc13lRg54t3z341RowQHIeRr4RJGvp+TOJL7ztvJkGK/QhaX8RCAYLLLvMjXRdsaxghDiKchlhBQi0lGBGOwhRhh/ElL7fV01Vm5H3kpwTFCVEAH0VJAsxQIXwAwAjfYVzv8mLrIFDSssM2JQBsCmNEHMCn6nA6uDqYIVIF05SABHGjXMBVVW9ZbeRx4AcxjvTa2KaQD4SFSU98taPgCmtFhFI+U6DNsG2BQUT83AV5V9NH5trBHRkGei2s4gKAjWO7gJvqzGpnTTH3H8w+bI96ifucN5smP9tMxXrlJEWGl9Ym4MgwdcMrb4q2NbAIUZJqa4hYV2ONXCW/D+hqMdYYhbmuioIejnPccsvK0xWt59i02tSgx7oFGR3vBvK+5jVxYtS2KL2QEbGKdfAaackyZg7DOisWCCdpH1lhwQpFufFdN1/v2GZ/S+u9LUo8DcAcXdmkjC4pqSRGrjpFDhnZcs8MRsce4kcwWrtGhgmSfhb7gq0Lp6apyunpLOVnZ7PEzEvmG0bdCjk67qX8bFMkZp4pK3agNW2q6fXeEGZnjQbNzJg7dsx/s/d1fpge7pbRteq/490iOvbwWElwqAky8OMBwr2TsOP3i4EyNnqz6GX72F2Prj+XD9Xm5O40Ew5FLqej0WjHLKvNPi+zTzU75+xpeogoztvHQ/M867xWVM2HvGTH6aoYxORg5YSb/NhUGRx25iliMJOVgdUumCxus05MHKNOo01dldnkEueQPuTZrpmp093cO4KlvCma6ZHHrzwrVrBNw9wbBxa3HGIKrpwfKw4RznIxy5pmy7o63NM6Y8304PlIzzfg3aLl4ZEFBxjyEgaZ2YdNUcnglvnjvFW7rba0kLg2a/TliN/c4U7iLbwlzYvni7vq1PD0v3h18ZGtTwW9WEHmlcyDTwzyZqyvzSLR5CQa7mhotxvtIn3XdS9m8PXiP0Zt+7d6fW92RrGOXuLZKwgPhpXEevcS/+Rdnv8zY7OepTFKB2zd9b4vkIGZ894oPasfiJ8z9sc/dvghT+//YTfEqDOqsMSuq1PiutEp2fpTxQDOOXzz80ZrWlc5/f3HGg0fPgacY89XhL+XGYqMhi3nwHeizrJESJcml2nPoYnJrdAwSeQutd0xE3Oo0csj6DZdYCeC3j9/AbRFpTCZEwAA"
        ),
        new
        (
            "Cosmo Sharp",
            "DS1H4sIAAAAAAAACqVYS3PaSBD+Ky6d2ZQeoxe32OzGh2TLZXsr69xkGIMWwbBC4Diu/Pf0TM9bgl2AC7To/qaf38zoPaiCcfQhHAXPwfg9+DsYF1x4Et8/R8FUPpgF45B/U6kVSq3wQwpaL4AxCubBOOOPFxKxlrbVs9T/x1uCiCWW0qyRWiupRRyttXyaOU/Z4NPNIMK/wTiO+ZMW/BM/ttKwgwdi6Z30bS9tX6XCd+nam44+taL/MZiTqpKPca0n/MFTyiDS9+CRfu+0YRmWRZiU0rwkKZfJKPgmxCzMC5KMgq8isQDBbSf1tnpu6ExjJHlG8jCTGFoSEGkYAUphQXyt1zP2ej03vidlHKVZUUgASxYQkYDIi8wCuVnUzczG4H5GSa6yY8mIwX1Ki9KCuGOb3caCiCInDCMKgDjORW4sgGvWzmir7Y2GsDeisE/SuCRR3DN/WFSQjV4dQ2kWCgOdRjD7o61W1HabCE/VskZEt0XYhb2uBLhle9paRYwx7blyX4vofh6npAyTPs7HaVfvzXDGmfjoLGhRwBDRbLHtzmPdNU48ptoYkJExIiwt6UN4npiSI44WMSD8DMDcsKapNlsrNScjfaHr3XXVXtJcD9MW/Hh2QMKM60WqRZSEDS7qEw8hfGo5D6pZhcECQzUnBWZTjQmWJ8JJg3+ztMxL4oP5rUOKWLS3wlQF+ibp+BiWVzQkHO6A5COeqfQg1M2CTpdfqnZ5TnwyTU0NY+jkKFWrCggiqUQRGlJkPgjhR8NHL0kVkglOIGUlb+3IpoRd17G1iQUNYjVLyIuq76DcPrWifa88LjElghAS7QXWLunBeLEQXYsnlxAESt6jt1ta2ex48gyhfY+ljpIswXwnPRifpFTmEURJWF+9cZj60k3VVh2zonGn2RnmpFcWbd+LRld0sMADrKCQvID0mrLCUsKArNIAa8gUAdg93dY/6Ke23pw/PQbjDFLwIU7nAknc1uweJ1ot2ba+58eKG+N2mroIfnsdb1LczzyIv9YvbLqzd53QTaARD/C9DeJvhchZqqxGxJh4v8ecpTTWhE2X9Xp+19J9TV/P2zzMkUWi/b7adG8XnvvuGtZ9rtd0aw6wgknK4VlWLdO3d4su+unJrY5kR9xjHYDbetuxORx/TKtyv7NEYWClYoWBTXgIxG+/XCQhU3XnZQafNNH2agV1byjS3IXHKA4kT6Vdy9bzs4+2FtLner7ozuRNAXN/8V3BwHxsuktuDPzy80AbOu2offc4aSpivGDxqWir+aRlm8eqndPuzH7+s9rfQoYbJ8sndiNg4J0MZtQHU4Mhvp1DGE6GZz6pV3axDlxm5A2RH5TZrGrQ+jRTfi+H60cwDiZV3bxd3bNdx8f66rerG7ZdsSu4V7WbAF4f4J1XX4kPIGMmzDairkHq1GfnC95M/A+tWf/GrqpoadH/yDNqvWgs2EbNhirxxC+tO+9FmofqRGFjLnq5zgZWrntaetLwyGbpqpcsfEW+Seqda2D15YG4oV0trcYg+qkunTEwTKwWJQNw5mQNM+Atazun3u2YoAl/Q9JLjjk3ZfoNCvCq8tBJDrwD8soC++PA2uaMWWjuJhoTLhSWrtkEC83N+tTH0aUmqMIWA09//gLUznSWdRMAAA=="
        )
    ];

    public static ReadOnlySeString BoxedLetterR
    {
        get
        {
            if (!field.IsEmpty)
                return field;

            using var rented = new RentedSeStringBuilder();
            rented.Builder
                  .PushColorType(34)
                  .Append(SeIconChar.BoxedLetterR.ToIconString())
                  .PopColorType();

            return field = rented.Builder.ToReadOnlySeString();
        }
    }

    public static ReadOnlySeString BoxedLetterD
    {
        get
        {
            if (!field.IsEmpty)
                return field;

            using var rented = new RentedSeStringBuilder();
            rented.Builder
                  .PushColorType(34)
                  .Append(SeIconChar.BoxedLetterD.ToIconString())
                  .PopColorType();

            return field = rented.Builder.ToReadOnlySeString();
        }
    }

    public static ReadOnlySeString BoxedLettersDR
    {
        get
        {
            if (!field.IsEmpty)
                return field;

            using var rented = new RentedSeStringBuilder();
            rented.Builder
                  .PushColorType(34)
                  .Append(SeIconChar.BoxedLetterD.ToIconString())
                  .Append(SeIconChar.BoxedLetterR.ToIconString())
                  .PopColorType();

            return field = rented.Builder.ToReadOnlySeString();
        }
    }

    public static ReadOnlySeString BracketDailyRoutines
    {
        get
        {
            if (!field.IsEmpty)
                return field;

            using var rented = new RentedSeStringBuilder();
            rented.Builder
                  .PushColorType(34)
                  .Append("[Daily Routines]")
                  .PopColorType();

            return field = rented.Builder.ToReadOnlySeString();
        }
    }

    public static ReadOnlySeString BracketDR
    {
        get
        {
            if (!field.IsEmpty)
                return field;

            using var rented = new RentedSeStringBuilder();
            rented.Builder
                  .PushColorType(34)
                  .Append("[DR]")
                  .PopColorType();

            return field = rented.Builder.ToReadOnlySeString();
        }
    }
}
