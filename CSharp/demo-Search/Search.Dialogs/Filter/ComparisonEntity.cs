using Microsoft.Bot.Builder.Luis.Models;

namespace Search.Dialogs.Filter
{
    class ComparisonEntity
    {
        public EntityRecommendation Entity;
        public EntityRecommendation Operator;
        public EntityRecommendation Lower;
        public EntityRecommendation Upper;
        public EntityRecommendation Property;

        public ComparisonEntity(EntityRecommendation comparison)
        {
            Entity = comparison;
        }

        public void AddEntity(EntityRecommendation entity)
        {
            if (entity.Type != "Comparison" && entity.StartIndex >= Entity.StartIndex && entity.EndIndex <= Entity.EndIndex)
            {
                switch (entity.Type)
                {
                    case "Currency": AddNumber(entity); break;
                    case "Value": AddNumber(entity); break;
                    case "Dimension": AddNumber(entity); break;
                    case "Operators": Operator = entity; break;
                    case "Properties": Property = entity; break;
                }
            }
        }

        private void AddNumber(EntityRecommendation entity)
        {
            if (Lower == null)
            {
                Lower = entity;
            }
            else if (entity.StartIndex < Lower.StartIndex)
            {
                Upper = Lower;
                Lower = entity;
            }
            else
            {
                Upper = entity;
            }
        }
    }
}