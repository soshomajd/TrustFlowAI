// SPDX-License-Identifier: MIT
pragma solidity 0.8.36;

import {IERC20} from "@openzeppelin/contracts/token/ERC20/IERC20.sol";
import {SafeERC20} from "@openzeppelin/contracts/token/ERC20/utils/SafeERC20.sol";
import {ReentrancyGuard} from "@openzeppelin/contracts/utils/ReentrancyGuard.sol";

contract TrustFlowEscrow is ReentrancyGuard {
    using SafeERC20 for IERC20;

    enum EscrowState {
        AwaitingFunding,
        Funded,
        Completed,
        Cancelled
    }

    struct Milestone {
        uint256 amount;
        bool released;
    }

    IERC20 public immutable token;
    address public immutable client;
    address public immutable freelancer;
    uint256 public immutable totalAmount;

    uint256 public releasedAmount;
    uint256 public nextMilestoneIndex;

    bool public workStarted;

    EscrowState public escrowState;

    Milestone[] private _milestones;

    error ZeroAddress();
    error ClientAndFreelancerMustDiffer();
    error NoMilestones();
    error InvalidMilestoneAmount(uint256 milestoneIndex);
    error OnlyClient();
    error OnlyFreelancer();
    error InvalidEscrowState(  EscrowState expected, EscrowState actual);
    error UnexpectedFundingAmount( uint256 expected, uint256 received);
    error InvalidMilestoneIndex(uint256 milestoneIndex);
    error WorkAlreadyStarted();
    error WorkNotStarted();
    error UnexpectedMilestoneIndex(uint256 expected,uint256 provided);
    error MilestoneAlreadyReleased( uint256 milestoneIndex);
    error CancellationNotAllowed(EscrowState currentState);

error CancellationNotAllowedAfterWorkStarted();


    event Funded(  address indexed client, uint256 amount);

    event WorkStarted(
        address indexed freelancer
    );

    event MilestoneReleased(
        uint256 indexed milestoneIndex,
        uint256 amount,
        address indexed freelancer
    );

    event EscrowCompleted(
        uint256 totalReleased
    );

    event EscrowCancelled(
        uint256 refundedAmount
    );

    modifier onlyClient() {
        if (msg.sender != client) {
            revert OnlyClient();
        }

        _;
    }

    modifier onlyFreelancer() {
        if (msg.sender != freelancer) {
            revert OnlyFreelancer();
        }

        _;
    }

    constructor(
        address token_,
        address client_,
        address freelancer_,
        uint256[] memory milestoneAmounts_
    ) {
        if (
            token_ == address(0) ||
            client_ == address(0) ||
            freelancer_ == address(0)
        ) {
            revert ZeroAddress();
        }

        if (client_ == freelancer_) {
            revert ClientAndFreelancerMustDiffer();
        }

        uint256 milestoneCount =
            milestoneAmounts_.length;

        if (milestoneCount == 0) {
            revert NoMilestones();
        }

        token = IERC20(token_);
        client = client_;
        freelancer = freelancer_;

        uint256 calculatedTotalAmount;

        for (
            uint256 i = 0;
            i < milestoneCount;
            i++
        ) {
            uint256 amount =
                milestoneAmounts_[i];

            if (amount == 0) {
                revert InvalidMilestoneAmount(i);
            }

            _milestones.push(
                Milestone({
                    amount: amount,
                    released: false
                })
            );

            calculatedTotalAmount += amount;
        }

        totalAmount = calculatedTotalAmount;

        escrowState =
            EscrowState.AwaitingFunding;
    }

    function getMilestoneCount() external view  returns (uint256)
         {
                return _milestones.length;
         }


    function getMilestone( uint256 milestoneIndex) external  view  returns (
        uint256 amount,
        bool released
    )
{
    if (
        milestoneIndex >=
        _milestones.length
    ) {
        revert InvalidMilestoneIndex(
            milestoneIndex
        );
    }

    Milestone storage milestone =
        _milestones[milestoneIndex];

    return (
        milestone.amount,
        milestone.released
    );
}


function remainingAmount() external view returns (uint256)
{
    return totalAmount - releasedAmount;
}


function fund() external onlyClient nonReentrant{
    if (
        escrowState !=
        EscrowState.AwaitingFunding
    ) {
        revert InvalidEscrowState(
            EscrowState.AwaitingFunding,
            escrowState
        );
    }

    uint256 balanceBefore =
        token.balanceOf(address(this));

    escrowState =
        EscrowState.Funded;

    token.safeTransferFrom(
        client,
        address(this),
        totalAmount
    );

    uint256 balanceAfter =
        token.balanceOf(address(this));

    if (balanceAfter < balanceBefore) {
        revert UnexpectedFundingAmount(
            totalAmount,
            0
        );
    }

    uint256 receivedAmount =
        balanceAfter - balanceBefore;

    if (receivedAmount != totalAmount) {
        revert UnexpectedFundingAmount(
            totalAmount,
            receivedAmount
        );
    }

    emit Funded(
        client,
        totalAmount
    );
}

function startWork() external onlyFreelancer {
    if (
        escrowState !=
        EscrowState.Funded
    ) {
        revert InvalidEscrowState(
            EscrowState.Funded,
            escrowState
        );
    }

    if (workStarted) {
        revert WorkAlreadyStarted();
    }

    workStarted = true;

    emit WorkStarted(
        freelancer
    );
}

function releaseMilestone(uint256 milestoneIndex)external onlyClient nonReentrant
{
    if (
        escrowState !=
        EscrowState.Funded
    ) {
        revert InvalidEscrowState(
            EscrowState.Funded,
            escrowState
        );
    }

    if (!workStarted) {
        revert WorkNotStarted();
    }


if (
    milestoneIndex >=
    _milestones.length
) {
    revert InvalidMilestoneIndex(
        milestoneIndex
    );
}

Milestone storage milestone =
    _milestones[milestoneIndex];

if (milestone.released) {
    revert MilestoneAlreadyReleased(
        milestoneIndex
    );
}

if (
    milestoneIndex !=
    nextMilestoneIndex
) {
    revert UnexpectedMilestoneIndex(
        nextMilestoneIndex,
        milestoneIndex
    );
}

    uint256 amount =
        milestone.amount;

    milestone.released = true;

    releasedAmount += amount;

    nextMilestoneIndex++;

    bool completed =
        nextMilestoneIndex ==
        _milestones.length;

    if (completed) {
        escrowState =
            EscrowState.Completed;
    }

    token.safeTransfer(
        freelancer,
        amount
    );

    emit MilestoneReleased(
        milestoneIndex,
        amount,
        freelancer
    );

    if (completed) {
        emit EscrowCompleted(
            releasedAmount
        );
    }
}

function cancelBeforeWorkStarts() external onlyClient  nonReentrant
{
    if (workStarted) {
        revert CancellationNotAllowedAfterWorkStarted();
    }

    if (
        escrowState != EscrowState.AwaitingFunding &&
        escrowState != EscrowState.Funded
    ) {
        revert CancellationNotAllowed(
            escrowState
        );
    }

    uint256 refundAmount;

    if (
        escrowState ==
        EscrowState.Funded
    ) {
        refundAmount =
            totalAmount - releasedAmount;
    }

    escrowState =
        EscrowState.Cancelled;

    if (refundAmount > 0) {
        token.safeTransfer(
            client,
            refundAmount
        );
    }

    emit EscrowCancelled(
        refundAmount
    );
}


}