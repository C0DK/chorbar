using Prometheus;

namespace Chorbar.Utils;

public sealed class AuthMetrics
{
    private readonly Counter _otpSent = Metrics.CreateCounter(
        "chorbar_otp_sent_total",
        "OTP emails requested."
    );

    private readonly Counter _otpVerified = Metrics.CreateCounter(
        "chorbar_otp_verified_total",
        "OTP verification attempts, by outcome.",
        new CounterConfiguration { LabelNames = ["outcome"] }
    );

    public void OtpRequested() => _otpSent.Inc();

    public void OtpResult(string outcome) => _otpVerified.WithLabels(outcome).Inc();
}
