using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using CMS.Commerce;
using CMS.DataEngine;
using CMS.DataEngine.Query;
using CMS.Helpers;

using Kentico.Xperience.Admin.DigitalCommerce;

using Microsoft.Extensions.Localization;

namespace DancingGoat.Commerce;

/// <summary>
/// Service for retrieving upsell order discount messages based on current cart subtotal and available promotions.
/// </summary>
/// <remarks>
/// This service analyzes active order promotions and generates messages encouraging customers to spend more
/// to qualify for the next available discount promotion.
/// </remarks>
public class UpsellOrderDiscountService
{
    private readonly IServiceProvider serviceProvider;
    private readonly IInfoProvider<PromotionInfo> promotionInfoProvider;
    private readonly IInfoProvider<PromotionCouponInfo> promotionCouponInfoProvider;
    private readonly IPriceFormatter priceFormatter;
    private readonly IStringLocalizer<DancingGoatShoppingCartController> localizer;


    public UpsellOrderDiscountService(
        IServiceProvider serviceProvider,
        IInfoProvider<PromotionInfo> promotionInfoProvider,
        IInfoProvider<PromotionCouponInfo> promotionCouponInfoProvider,
        IPriceFormatter priceFormatter,
        IStringLocalizer<DancingGoatShoppingCartController> localizer)
    {
        this.serviceProvider = serviceProvider;
        this.promotionInfoProvider = promotionInfoProvider;
        this.promotionCouponInfoProvider = promotionCouponInfoProvider;
        this.priceFormatter = priceFormatter;
        this.localizer = localizer;
    }


    /// <summary>
    /// Gets an upsell message encouraging the customer to spend more to qualify for the next available order discount promotion.
    /// </summary>
    /// <param name="subtotalAfterLineDiscount">The current cart subtotal amount after line discounts (catalog discounts) were applied.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <example>
    /// Example return value: "Spend $25.00 more and get 10% discount."
    /// </example>
    public async Task<string> GetUpsellOrderDiscountMessage(decimal subtotalAfterLineDiscount, CancellationToken cancellationToken)
    {
        var (nextOrderDiscountRemainingTreshold, nextOrderDiscountValue) = await GetNextEligibleOrderPromotion(subtotalAfterLineDiscount, cancellationToken);

        if ((nextOrderDiscountRemainingTreshold > 0) && !string.IsNullOrEmpty(nextOrderDiscountValue))
        {
            var nextDiscountTextResource = localizer["Spend {0} more and get {1} discount."];
            var nextDiscountText = string.Format(nextDiscountTextResource, priceFormatter.Format(nextOrderDiscountRemainingTreshold, new PriceFormatContext()), nextOrderDiscountValue);
            return nextDiscountText;
        }

        return null;
    }


    /// <summary>
    /// Gets the next eligible order promotion that the customer can qualify for by spending more.
    /// </summary>
    /// <param name="subtotalAfterLineDiscount">The current cart subtotal amount after line discounts (catalog discounts) were applied.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>
    /// A tuple containing:
    /// - Item1: The amount the customer needs to spend more to qualify (decimal).
    /// - Item2: A formatted label representing the discount value (e.g., "10%" or "$25.00").
    /// Returns default tuple (0, null) if no eligible promotion is found.
    /// </returns>
    private async Task<(decimal, string)> GetNextEligibleOrderPromotion(decimal subtotalAfterLineDiscount, CancellationToken cancellationToken)
    {
        var activeOrderPromotions = await GetActiveOrderPromotions(cancellationToken);
        var promotionRuleProperties = ExtractPromotionRuleProperties(activeOrderPromotions);

        var fixedValuesBasedPromotionProperties = promotionRuleProperties
                                                        .Where(p => p.MinimumRequirementValueType == MinimumRequirementValueType.Price)
                                                        .OrderBy(p => p.MinimumRequirementValue);

        var nextAvailablePromotion = fixedValuesBasedPromotionProperties.FirstOrDefault(p => p.MinimumRequirementValue > subtotalAfterLineDiscount);

        if (nextAvailablePromotion == null)
        {
            return default;
        }

        var label = GetNextDiscountValueLabel(nextAvailablePromotion);
        var amountToSpend = nextAvailablePromotion.MinimumRequirementValue - subtotalAfterLineDiscount;

        return (amountToSpend, label);
    }


    /// <summary>
    /// Gets the next discount value label for the next eligible order promotion.
    /// </summary>
    /// <param name="promotionRuleProperties">The promotion rule properties of the next eligible order promotion.</param>
    /// <returns>The next discount value label.</returns>
    private static string GetNextDiscountValueLabel(OrderPromotionRuleProperties promotionRuleProperties)
    {
        var discountRule = new DancingGoatOrderPromotionRule();
        discountRule.Properties.DiscountValue = promotionRuleProperties.DiscountValue;
        discountRule.Properties.DiscountValueType = promotionRuleProperties.DiscountValueType;
        return discountRule.GetDiscountValueLabel();
    }


    /// <summary>
    /// Extracts order promotion rule properties from a collection of promotion information objects.
    /// </summary>
    /// <param name="promotions">Collection of promotion information objects.</param>
    /// <returns>
    /// A collection of <see cref="OrderPromotionRuleProperties"/> extracted from the promotion configurations.
    /// </returns>
    private static IEnumerable<OrderPromotionRuleProperties> ExtractPromotionRuleProperties(IEnumerable<PromotionInfo> promotions)
    {
        var promotionProperties = new List<OrderPromotionRuleProperties>();
        foreach (var promotion in promotions)
        {
            var promotionRuleProperties = promotion.GetPromotionRuleProperties<OrderPromotionRuleProperties>();

            if (promotionRuleProperties != null)
            {
                promotionProperties.Add(promotionRuleProperties);
            }
        }

        return promotionProperties;
    }


    /// <summary>
    /// Retrieves all active order promotions that match the DancingGoat order promotion rule identifier.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>
    /// A collection of <see cref="PromotionInfo"/> objects representing active order promotions.
    /// Only promotions that are currently active (within their active date range) and don't have a coupon defined are returned.
    /// </returns>
    private async Task<IEnumerable<PromotionInfo>> GetActiveOrderPromotions(CancellationToken cancellationToken)
    {
        DateTime currentTime = DateTime.Now;

        var couponSubquery = promotionCouponInfoProvider.Get()
            .Column(nameof(PromotionCouponInfo.PromotionCouponID))
            .WhereEquals(nameof(PromotionCouponInfo.PromotionCouponPromotionID), nameof(PromotionInfo.PromotionID).AsColumn());

        return await promotionInfoProvider.Get()
            // Order promotions only
            .WhereEquals(nameof(PromotionInfo.PromotionType), PromotionType.Order.ToStringRepresentation())
            // Only the specified promotion rule
            .WhereEquals(nameof(PromotionInfo.PromotionRuleIdentifier), DancingGoatOrderPromotionRule.IDENTIFIER)
            // Active promotions
            .WhereNotNull(nameof(PromotionInfo.PromotionActiveFromWhen))
            .WhereLessThan(nameof(PromotionInfo.PromotionActiveFromWhen), currentTime)
            .Where(new WhereCondition()
                .WhereNull(nameof(PromotionInfo.PromotionActiveToWhen))
                .Or()
                .WhereGreaterThan(nameof(PromotionInfo.PromotionActiveToWhen), currentTime)
            )
            // Only promotions without coupons
            .WhereNotExists(couponSubquery)
            .GetEnumerableTypedResultAsync(cancellationToken: cancellationToken);
    }
}
