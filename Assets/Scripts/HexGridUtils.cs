using UnityEngine;

public static class HexGridUtils
{
    public static readonly Vector3Int[] OddOffsets = {
        new Vector3Int(+1, 0, 0), new Vector3Int(0, +1, 0), new Vector3Int(-1, +1, 0),
        new Vector3Int(-1, 0, 0), new Vector3Int(-1, -1, 0), new Vector3Int(0, -1, 0)
    };

    public static readonly Vector3Int[] EvenOffsets = {
        new Vector3Int(+1, 0, 0), new Vector3Int(+1, +1, 0), new Vector3Int(0, +1, 0),
        new Vector3Int(-1, 0, 0), new Vector3Int(0, -1, 0), new Vector3Int(+1, -1, 0)
    };

    public static Vector3Int[] GetOffsets(Vector3Int cell)
    {
        return (cell.y % 2 != 0) ? EvenOffsets : OddOffsets;
    }

    public static bool IsNeighbor(Vector3Int cell1, Vector3Int cell2)
    {
        Vector3Int[] offsets = GetOffsets(cell1);
        foreach (var off in offsets)
            if (cell1 + off == cell2) return true;
        return false;
    }

    public static Vector3Int OffsetToCube(Vector3Int o)
    {
        int x = o.x - (o.y - (o.y & 1)) / 2;
        int z = o.y;
        int y = -x - z;
        return new Vector3Int(x, y, z);
    }

    public static float DistanceCube(Vector3Int a, Vector3Int b)
    {
        Vector3Int ac = OffsetToCube(a);
        Vector3Int bc = OffsetToCube(b);
        return (Mathf.Abs(ac.x - bc.x) + Mathf.Abs(ac.y - bc.y) + Mathf.Abs(ac.z - bc.z)) * 0.5f;
    }

    public static Vector3Int GetRawOppositeCell(Vector3Int centerCell, Vector3Int awayFromCell)
    {
        Vector3Int[] offsets = GetOffsets(centerCell);
        for (int i = 0; i < 6; i++)
            if (centerCell + offsets[i] == awayFromCell)
                return centerCell + offsets[(i + 3) % 6];
        return centerCell;
    }

    /// <summary>
    /// Uzak mesafeli saldırılarda knockback yönünü hesaplar.
    /// awayFromCell bitişik olmasa bile, yön vektörüne göre en uygun karşı komşuyu bulur.
    /// </summary>
    public static Vector3Int GetKnockbackCellRanged(Vector3Int enemyCell, Vector3Int playerCell)
    {
        // Önce normal adjacent kontrolü dene
        Vector3Int normal = GetRawOppositeCell(enemyCell, playerCell);
        if (normal != enemyCell) return normal;

        // Uzak mesafe: cube koordinatlarıyla yön hesapla
        Vector3Int eCube = OffsetToCube(enemyCell);
        Vector3Int pCube = OffsetToCube(playerCell);
        // Yön vektörü: player → enemy
        float dx = eCube.x - pCube.x;
        float dy = eCube.y - pCube.y;
        float dz = eCube.z - pCube.z;

        // Düşmanın 6 komşusundan, player'dan en uzak olanı seç
        Vector3Int[] offsets = GetOffsets(enemyCell);
        Vector3Int bestCell = enemyCell;
        float bestDot = float.MinValue;

        for (int i = 0; i < 6; i++)
        {
            Vector3Int neighbor = enemyCell + offsets[i];
            Vector3Int nCube = OffsetToCube(neighbor);
            // Bu komşunun player → enemy yönüne ne kadar hizalı olduğunu ölç (dot product)
            float ndx = nCube.x - eCube.x;
            float ndy = nCube.y - eCube.y;
            float ndz = nCube.z - eCube.z;
            float dot = ndx * dx + ndy * dy + ndz * dz;
            if (dot > bestDot) { bestDot = dot; bestCell = neighbor; }
        }

        return bestCell;
    }
}
