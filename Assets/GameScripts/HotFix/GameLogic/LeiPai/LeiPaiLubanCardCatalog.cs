using System.Linq;

namespace GameLogic.LeiPai
{
    public static class LeiPaiLubanCardCatalog
    {
        public static LeiPaiCardCatalog Load()
            => new LeiPaiCardCatalog(ConfigSystem.Instance.Tables.TbCard.DataList.Select(card =>
                new LeiPaiCardConfig(card.Id, card.Key, card.Category, card.Name, card.Icon,
                    card.Description, card.ClueRole, card.IsSafe, card.Effects)));
    }
}
