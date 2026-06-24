using FluentAssertions;
using JobRadar.Domain.Entities;
using Xunit;

namespace JobRadar.UnitTests;

public class ApplicationStatusTransitionsTests
{
    [Fact]
    public void Forward_move_along_the_pipeline_is_allowed()
        => ApplicationStatusTransitions.CanTransition(ApplicationStatus.Submitted, ApplicationStatus.InterviewScheduled)
            .Should().BeTrue();

    [Fact]
    public void Backward_move_along_the_pipeline_is_rejected()
        => ApplicationStatusTransitions.CanTransition(ApplicationStatus.InterviewScheduled, ApplicationStatus.Submitted)
            .Should().BeFalse();

    [Fact]
    public void Withdrawing_from_an_active_stage_is_allowed()
        => ApplicationStatusTransitions.CanTransition(ApplicationStatus.UnderReview, ApplicationStatus.Withdrawn)
            .Should().BeTrue();

    [Fact]
    public void Rejecting_from_an_active_stage_is_allowed()
        => ApplicationStatusTransitions.CanTransition(ApplicationStatus.Submitted, ApplicationStatus.Rejected)
            .Should().BeTrue();

    [Fact]
    public void Leaving_a_terminal_state_is_rejected()
        => ApplicationStatusTransitions.CanTransition(ApplicationStatus.Withdrawn, ApplicationStatus.UnderReview)
            .Should().BeFalse();

    [Fact]
    public void Transition_to_the_same_status_is_rejected()
        => ApplicationStatusTransitions.CanTransition(ApplicationStatus.Submitted, ApplicationStatus.Submitted)
            .Should().BeFalse();

    [Fact]
    public void Rejected_is_terminal()
        => ApplicationStatusTransitions.IsTerminal(ApplicationStatus.Rejected).Should().BeTrue();

    [Fact]
    public void Submitted_is_not_terminal()
        => ApplicationStatusTransitions.IsTerminal(ApplicationStatus.Submitted).Should().BeFalse();
}
