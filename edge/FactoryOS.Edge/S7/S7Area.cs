namespace FactoryOS.Edge.S7;

/// <summary>The Siemens S7 memory area a variable is read from.</summary>
public enum S7Area
{
    /// <summary>A data block (<c>DBx</c>); the block number is significant.</summary>
    DataBlock,

    /// <summary>The process input image (<c>I</c>/<c>E</c>).</summary>
    Input,

    /// <summary>The process output image (<c>Q</c>/<c>A</c>).</summary>
    Output,

    /// <summary>The bit-memory / flag area (<c>M</c>/<c>Merker</c>).</summary>
    Memory,
}
