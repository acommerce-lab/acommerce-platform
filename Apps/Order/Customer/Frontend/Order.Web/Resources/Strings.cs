using System.Resources;
using System.Globalization;

namespace Order.Web.Resources;

/// <summary>
/// Strongly-typed accessor for localized strings from .resx files.
/// Resource files: Strings.resx (en, default), Strings.ar.resx (ar).
/// Uses ResourceManager with CultureInfo.CurrentUICulture.
/// </summary>
public static class Strings
{
    private static readonly ResourceManager ResourceManager =
        new ResourceManager("Order.Web.Resources.Strings", typeof(Strings).Assembly);

    public static string GetString(string key, CultureInfo? culture = null)
    {
        return ResourceManager.GetString(key, culture ?? CultureInfo.CurrentUICulture) ?? key;
    }

    // Common
    public static string app_name => GetString(nameof(app_name));
    public static string common_loading => GetString(nameof(common_loading));
    public static string common_loading_dots => GetString(nameof(common_loading_dots));
    public static string common_error => GetString(nameof(common_error));
    public static string common_signin => GetString(nameof(common_signin));
    public static string common_signout => GetString(nameof(common_signout));
    public static string common_home => GetString(nameof(common_home));
    public static string common_back => GetString(nameof(common_back));
    public static string common_send => GetString(nameof(common_send));
    public static string common_save => GetString(nameof(common_save));
    public static string common_all => GetString(nameof(common_all));
    public static string common_optional => GetString(nameof(common_optional));

    // Navigation
    public static string nav_brand => GetString(nameof(nav_brand));
    public static string nav_home => GetString(nameof(nav_home));
    public static string nav_search => GetString(nameof(nav_search));
    public static string nav_orders => GetString(nameof(nav_orders));
    public static string nav_messages => GetString(nameof(nav_messages));
    public static string nav_cart => GetString(nameof(nav_cart));
    public static string nav_profile => GetString(nameof(nav_profile));
    public static string nav_signin => GetString(nameof(nav_signin));

    // Home
    public static string home_title => GetString(nameof(home_title));
    public static string home_subtitle => GetString(nameof(home_subtitle));
    public static string home_all => GetString(nameof(home_all));
    public static string home_loading => GetString(nameof(home_loading));
    public static string home_empty => GetString(nameof(home_empty));
    public static string home_signin => GetString(nameof(home_signin));
    public static string home_language_toggle => GetString(nameof(home_language_toggle));

    // Settings
    public static string settings_title => GetString(nameof(settings_title));
    public static string settings_theme => GetString(nameof(settings_theme));
    public static string settings_theme_light => GetString(nameof(settings_theme_light));
    public static string settings_theme_dark => GetString(nameof(settings_theme_dark));
    public static string settings_language => GetString(nameof(settings_language));
    public static string settings_language_ar => GetString(nameof(settings_language_ar));
    public static string settings_language_en => GetString(nameof(settings_language_en));
    public static string settings_about => GetString(nameof(settings_about));
    public static string settings_version => GetString(nameof(settings_version));
    public static string settings_sign_out => GetString(nameof(settings_sign_out));
    public static string settings_terms => GetString(nameof(settings_terms));

    // Cart
    public static string cart_page_title => GetString(nameof(cart_page_title));
    public static string cart_title => GetString(nameof(cart_title));
    public static string cart_empty_title => GetString(nameof(cart_empty_title));
    public static string cart_empty_message => GetString(nameof(cart_empty_message));
    public static string cart_browse => GetString(nameof(cart_browse));
    public static string cart_pickup_hint => GetString(nameof(cart_pickup_hint));
    public static string cart_subtotal => GetString(nameof(cart_subtotal));
    public static string cart_total => GetString(nameof(cart_total));
    public static string cart_checkout => GetString(nameof(cart_checkout));
    public static string cart_clear => GetString(nameof(cart_clear));

    // Checkout
    public static string checkout_page_title => GetString(nameof(checkout_page_title));
    public static string checkout_signin_first => GetString(nameof(checkout_signin_first));
    public static string checkout_cart_empty => GetString(nameof(checkout_cart_empty));
    public static string checkout_browse_offers => GetString(nameof(checkout_browse_offers));
    public static string checkout_title => GetString(nameof(checkout_title));
    public static string checkout_summary => GetString(nameof(checkout_summary));
    public static string checkout_total => GetString(nameof(checkout_total));
    public static string checkout_pickup => GetString(nameof(checkout_pickup));
    public static string checkout_instore => GetString(nameof(checkout_instore));
    public static string checkout_curbside => GetString(nameof(checkout_curbside));
    public static string checkout_car_details => GetString(nameof(checkout_car_details));
    public static string checkout_car_model => GetString(nameof(checkout_car_model));
    public static string checkout_car_color => GetString(nameof(checkout_car_color));
    public static string checkout_car_plate => GetString(nameof(checkout_car_plate));
    public static string checkout_payment => GetString(nameof(checkout_payment));
    public static string checkout_payment_hint => GetString(nameof(checkout_payment_hint));
    public static string checkout_cash => GetString(nameof(checkout_cash));
    public static string checkout_card => GetString(nameof(checkout_card));
    public static string checkout_cash_tendered => GetString(nameof(checkout_cash_tendered));
    public static string checkout_change => GetString(nameof(checkout_change));
    public static string checkout_not_enough => GetString(nameof(checkout_not_enough));
    public static string checkout_notes => GetString(nameof(checkout_notes));
    public static string checkout_submit => GetString(nameof(checkout_submit));
    public static string checkout_submitting => GetString(nameof(checkout_submitting));
    public static string checkout_back_to_cart => GetString(nameof(checkout_back_to_cart));
    public static string checkout_err_car_details => GetString(nameof(checkout_err_car_details));
    public static string checkout_err_cash_cover => GetString(nameof(checkout_err_cash_cover));
    public static string checkout_err_create => GetString(nameof(checkout_err_create));

    // Favorites
    public static string favorites_page_title => GetString(nameof(favorites_page_title));
    public static string favorites_signin_title => GetString(nameof(favorites_signin_title));
    public static string favorites_signin_action => GetString(nameof(favorites_signin_action));
    public static string favorites_title => GetString(nameof(favorites_title));
    public static string favorites_empty_title => GetString(nameof(favorites_empty_title));
    public static string favorites_empty_message => GetString(nameof(favorites_empty_message));
    public static string favorites_browse => GetString(nameof(favorites_browse));

    // Login
    public static string login_page_title => GetString(nameof(login_page_title));
    public static string login_brand_name => GetString(nameof(login_brand_name));
    public static string login_brand_tagline => GetString(nameof(login_brand_tagline));
    public static string login_phone_title => GetString(nameof(login_phone_title));
    public static string login_phone_subtitle => GetString(nameof(login_phone_subtitle));
    public static string login_phone_label => GetString(nameof(login_phone_label));
    public static string login_request_otp => GetString(nameof(login_request_otp));
    public static string login_sending => GetString(nameof(login_sending));
    public static string login_otp_title => GetString(nameof(login_otp_title));
    public static string login_otp_subtitle => GetString(nameof(login_otp_subtitle));
    public static string login_otp_label => GetString(nameof(login_otp_label));
    public static string login_verify_otp => GetString(nameof(login_verify_otp));
    public static string login_verifying => GetString(nameof(login_verifying));
    public static string login_back => GetString(nameof(login_back));
    public static string login_demo_mode => GetString(nameof(login_demo_mode));
    public static string login_demo_hint => GetString(nameof(login_demo_hint));
    public static string login_demo_accounts => GetString(nameof(login_demo_accounts));

    // Orders
    public static string orders_page_title => GetString(nameof(orders_page_title));
    public static string orders_signin_title => GetString(nameof(orders_signin_title));
    public static string orders_title => GetString(nameof(orders_title));
    public static string orders_empty_title => GetString(nameof(orders_empty_title));
    public static string orders_empty_message => GetString(nameof(orders_empty_message));
    public static string orders_browse => GetString(nameof(orders_browse));
    public static string orders_pickup_curbside => GetString(nameof(orders_pickup_curbside));
    public static string orders_pickup_instore => GetString(nameof(orders_pickup_instore));
    public static string orders_payment_card => GetString(nameof(orders_payment_card));
    public static string orders_payment_cash => GetString(nameof(orders_payment_cash));

    // Notifications
    public static string notifications_page_title => GetString(nameof(notifications_page_title));
    public static string notifications_signin_title => GetString(nameof(notifications_signin_title));
    public static string notifications_title => GetString(nameof(notifications_title));
    public static string notifications_empty_title => GetString(nameof(notifications_empty_title));
    public static string notifications_empty_message => GetString(nameof(notifications_empty_message));
    public static string notifications_mark_all_read => GetString(nameof(notifications_mark_all_read));

    // Messages
    public static string messages_page_title => GetString(nameof(messages_page_title));
    public static string messages_signin_title => GetString(nameof(messages_signin_title));
    public static string messages_title => GetString(nameof(messages_title));
    public static string messages_empty_title => GetString(nameof(messages_empty_title));
    public static string messages_empty_message => GetString(nameof(messages_empty_message));

    // Chat
    public static string chat_page_title => GetString(nameof(chat_page_title));
    public static string chat_signin_title => GetString(nameof(chat_signin_title));
    public static string chat_title => GetString(nameof(chat_title));
    public static string chat_empty => GetString(nameof(chat_empty));
    public static string chat_placeholder => GetString(nameof(chat_placeholder));
    public static string chat_send => GetString(nameof(chat_send));

    // Offer Details
    public static string offer_page_title_fallback => GetString(nameof(offer_page_title_fallback));
    public static string offer_not_found => GetString(nameof(offer_not_found));
    public static string offer_back_home => GetString(nameof(offer_back_home));
    public static string offer_description => GetString(nameof(offer_description));
    public static string offer_store_info => GetString(nameof(offer_store_info));
    public static string offer_add_to_cart => GetString(nameof(offer_add_to_cart));
    public static string offer_chat => GetString(nameof(offer_chat));

    // Order Details
    public static string order_details_page_title => GetString(nameof(order_details_page_title));
    public static string order_success_title => GetString(nameof(order_success_title));
    public static string order_success_subtitle => GetString(nameof(order_success_subtitle));
    public static string order_total => GetString(nameof(order_total));
    public static string order_view => GetString(nameof(order_view));
    public static string order_continue => GetString(nameof(order_continue));
    public static string order_not_found => GetString(nameof(order_not_found));
    public static string order_all => GetString(nameof(order_all));
    public static string order_items => GetString(nameof(order_items));
    public static string order_vendor => GetString(nameof(order_vendor));
    public static string order_pickup => GetString(nameof(order_pickup));
    public static string order_payment => GetString(nameof(order_payment));
    public static string order_cash_tendered => GetString(nameof(order_cash_tendered));
    public static string order_expected_change => GetString(nameof(order_expected_change));
    public static string order_notes => GetString(nameof(order_notes));
    public static string order_cancel => GetString(nameof(order_cancel));

    // Profile
    public static string profile_page_title => GetString(nameof(profile_page_title));
    public static string profile_not_signed_in => GetString(nameof(profile_not_signed_in));
    public static string profile_default_name => GetString(nameof(profile_default_name));
    public static string profile_stat_orders => GetString(nameof(profile_stat_orders));
    public static string profile_stat_favorites => GetString(nameof(profile_stat_favorites));
    public static string profile_stat_points => GetString(nameof(profile_stat_points));
    public static string profile_section_account => GetString(nameof(profile_section_account));
    public static string profile_menu_orders => GetString(nameof(profile_menu_orders));
    public static string profile_menu_favorites => GetString(nameof(profile_menu_favorites));
    public static string profile_menu_messages => GetString(nameof(profile_menu_messages));
    public static string profile_menu_notifications => GetString(nameof(profile_menu_notifications));
    public static string profile_menu_account => GetString(nameof(profile_menu_account));
    public static string profile_section_app => GetString(nameof(profile_section_app));
    public static string profile_menu_settings => GetString(nameof(profile_menu_settings));

    // Account
    public static string account_page_title => GetString(nameof(account_page_title));
    public static string account_title => GetString(nameof(account_title));
    public static string account_subtitle => GetString(nameof(account_subtitle));
    public static string account_personal_info => GetString(nameof(account_personal_info));
    public static string account_full_name => GetString(nameof(account_full_name));
    public static string account_full_name_placeholder => GetString(nameof(account_full_name_placeholder));
    public static string account_email => GetString(nameof(account_email));
    public static string account_email_placeholder => GetString(nameof(account_email_placeholder));
    public static string account_car_details => GetString(nameof(account_car_details));
    public static string account_car_hint => GetString(nameof(account_car_hint));
    public static string account_car_model => GetString(nameof(account_car_model));
    public static string account_car_model_placeholder => GetString(nameof(account_car_model_placeholder));
    public static string account_car_color => GetString(nameof(account_car_color));
    public static string account_car_color_placeholder => GetString(nameof(account_car_color_placeholder));
    public static string account_car_plate => GetString(nameof(account_car_plate));
    public static string account_car_plate_placeholder => GetString(nameof(account_car_plate_placeholder));
    public static string account_save => GetString(nameof(account_save));
    public static string account_saving => GetString(nameof(account_saving));
    public static string account_name_required => GetString(nameof(account_name_required));
    public static string account_saved => GetString(nameof(account_saved));
    public static string account_save_failed => GetString(nameof(account_save_failed));

    // Legal
    public static string legal_page_title => GetString(nameof(legal_page_title));
    public static string legal_title => GetString(nameof(legal_title));
    public static string legal_subtitle => GetString(nameof(legal_subtitle));
    public static string legal_terms_of_use => GetString(nameof(legal_terms_of_use));
    public static string legal_terms_intro => GetString(nameof(legal_terms_intro));
    public static string legal_terms_age => GetString(nameof(legal_terms_age));
    public static string legal_terms_accuracy => GetString(nameof(legal_terms_accuracy));
    public static string legal_terms_illegal => GetString(nameof(legal_terms_illegal));
    public static string legal_terms_suspend => GetString(nameof(legal_terms_suspend));
    public static string legal_terms_prices => GetString(nameof(legal_terms_prices));
    public static string legal_privacy_title => GetString(nameof(legal_privacy_title));
    public static string legal_privacy_intro => GetString(nameof(legal_privacy_intro));
    public static string legal_privacy_data => GetString(nameof(legal_privacy_data));
    public static string legal_privacy_share => GetString(nameof(legal_privacy_share));
    public static string legal_privacy_car => GetString(nameof(legal_privacy_car));
    public static string legal_privacy_delete => GetString(nameof(legal_privacy_delete));
    public static string legal_refund_title => GetString(nameof(legal_refund_title));
    public static string legal_refund_intro => GetString(nameof(legal_refund_intro));
    public static string legal_refund_cancel => GetString(nameof(legal_refund_cancel));
    public static string legal_refund_after => GetString(nameof(legal_refund_after));
    public static string legal_refund_issue => GetString(nameof(legal_refund_issue));
    public static string legal_refund_money => GetString(nameof(legal_refund_money));

    // Search
    public static string search_page_title => GetString(nameof(search_page_title));
    public static string search_title => GetString(nameof(search_title));
    public static string search_placeholder => GetString(nameof(search_placeholder));
    public static string search_all => GetString(nameof(search_all));
    public static string search_map_view => GetString(nameof(search_map_view));
    public static string search_list_view => GetString(nameof(search_list_view));
    public static string search_loading => GetString(nameof(search_loading));
    public static string search_empty_title => GetString(nameof(search_empty_title));
    public static string search_empty_message => GetString(nameof(search_empty_message));
    public static string search_stores => GetString(nameof(search_stores));
    public static string search_offers => GetString(nameof(search_offers));
    public static string search_advanced_filters => GetString(nameof(search_advanced_filters));
    public static string search_discounted_only => GetString(nameof(search_discounted_only));
    public static string search_min_price => GetString(nameof(search_min_price));
    public static string search_max_price => GetString(nameof(search_max_price));
    public static string search_min_rating => GetString(nameof(search_min_rating));
    public static string search_chip_discount => GetString(nameof(search_chip_discount));
    public static string search_chip_clear_all => GetString(nameof(search_chip_clear_all));
    public static string search_view_menu => GetString(nameof(search_view_menu));
    public static string search_view_details => GetString(nameof(search_view_details));

    // Vendor
    public static string vendor_page_title_fallback => GetString(nameof(vendor_page_title_fallback));
    public static string vendor_loading => GetString(nameof(vendor_loading));
    public static string vendor_not_found => GetString(nameof(vendor_not_found));
    public static string vendor_home => GetString(nameof(vendor_home));
    public static string vendor_rating => GetString(nameof(vendor_rating));
    public static string vendor_store_info => GetString(nameof(vendor_store_info));
    public static string vendor_offers => GetString(nameof(vendor_offers));
    public static string vendor_no_offers => GetString(nameof(vendor_no_offers));
}
