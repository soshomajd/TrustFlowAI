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
        Cancelled,
        Terminated
    }

    // A case is opened by one side on the milestone that is next in line.
    // It never blocks the other side: the client can still release, and the
    // freelancer can still resign. It only starts the clock that lets the
    // arbiter settle that one milestone between the two of them.
    enum CaseKind {
        None,
        PaymentRequest,
        RefundRequest
    }

    struct Case {
        CaseKind kind;
        uint64 openedAt;
    }

    struct Milestone {
        uint256 amount;
        bool released;
    }

    IERC20 public immutable token;
    address public immutable client;
    address public immutable freelancer;
    uint256 public immutable totalAmount;

    // The only extra party. It can settle a case between the client and the
    // freelancer and nothing else: it can never send funds anywhere else.
    address public immutable arbiter;

    uint256 public constant RESPONSE_WINDOW = 14 days;
    uint256 public constant STALL_WINDOW = 90 days;

    // Paid to the freelancer / handed back to the client so far.
    uint256 public releasedAmount;
    uint256 public refundedAmount;
    uint256 public nextMilestoneIndex;

    bool public workStarted;

    EscrowState public escrowState;

    Milestone[] private _milestones;

    mapping(uint256 => uint256) private _paidToFreelancer;

    mapping(uint256 => bool) private _settledByCase;

    Case private _openCase;

    error ZeroAddress();
    error ClientAndFreelancerMustDiffer();
    error NoMilestones();
    error InvalidMilestoneAmount(uint256 milestoneIndex);
    error OnlyClient();
    error OnlyFreelancer();
    error OnlyArbiter();
    error ArbiterNotAllowed();
    error WorkNotStartedYet();
    error CaseAlreadyOpen(CaseKind kind);
    error NoOpenCase();
    error OnlyCaseOpener();
    error OnlyParticipant();
    error CaseMilestoneMismatch(uint256 expected, uint256 provided);
    error ResponseWindowNotElapsed(uint256 availableAt);
    error StallWindowNotElapsed(uint256 availableAt);
    error FreelancerAmountTooLarge(uint256 maximum, uint256 provided);
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

    event CaseOpened(
        uint256 indexed milestoneIndex,
        CaseKind kind,
        address indexed opener,
        uint256 openedAt
    );

    event CaseWithdrawn(
        uint256 indexed milestoneIndex,
        address indexed opener
    );

    event CaseSettled(
        uint256 indexed milestoneIndex,
        uint256 toFreelancer,
        uint256 toClient,
        bool escrowEnded,
        address indexed settledBy
    );

    event FreelancerResigned(
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

    modifier onlyArbiter() {
        if (msg.sender != arbiter) {
            revert OnlyArbiter();
        }

        _;
    }

    constructor(
        address token_,
        address client_,
        address freelancer_,
        address arbiter_,
        uint256[] memory milestoneAmounts_
    ) {
        if (
            token_ == address(0) ||
            client_ == address(0) ||
            freelancer_ == address(0) ||
            arbiter_ == address(0)
        ) {
            revert ZeroAddress();
        }

        if (client_ == freelancer_) {
            revert ClientAndFreelancerMustDiffer();
        }

        // An arbiter who is also a party would decide its own case.
        if (arbiter_ == client_ || arbiter_ == freelancer_) {
            revert ArbiterNotAllowed();
        }

        uint256 milestoneCount =
            milestoneAmounts_.length;

        if (milestoneCount == 0) {
            revert NoMilestones();
        }

        token = IERC20(token_);
        client = client_;
        freelancer = freelancer_;
        arbiter = arbiter_;

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
    return totalAmount - releasedAmount - refundedAmount;
}

// What the freelancer actually received for a milestone. It equals the
// milestone amount for a normal release and can be less after a settlement.
function getMilestonePaid(uint256 milestoneIndex) external view returns (uint256)
{
    if (milestoneIndex >= _milestones.length) {
        revert InvalidMilestoneIndex(milestoneIndex);
    }

    return _paidToFreelancer[milestoneIndex];
}

// True when the arbiter (or the stalled-case exit) closed this milestone,
// false when the client released it.
function isSettledByCase(uint256 milestoneIndex) external view returns (bool)
{
    if (milestoneIndex >= _milestones.length) {
        revert InvalidMilestoneIndex(milestoneIndex);
    }

    return _settledByCase[milestoneIndex];
}

// The open case, if any. It always belongs to nextMilestoneIndex.
function getOpenCase() external view returns (CaseKind kind, uint256 openedAt)
{
    return (_openCase.kind, _openCase.openedAt);
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

    _paidToFreelancer[milestoneIndex] = amount;

    releasedAmount += amount;

    delete _openCase;

    nextMilestoneIndex++;

    bool completed =
        nextMilestoneIndex ==
        _milestones.length;

    if (completed) {
        escrowState =
            EscrowState.Completed;
    }

    _payFreelancer(milestoneIndex, amount);

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
            totalAmount - releasedAmount - refundedAmount;
    }

    refundedAmount += refundAmount;

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


// The freelancer says the milestone is delivered and asks to be paid. The
// client can still release at any time; if they stay silent, the arbiter can
// settle the milestone after RESPONSE_WINDOW.
function requestPayment() external onlyFreelancer
{
    _openCaseOf(CaseKind.PaymentRequest);
}

// The client says the milestone was not delivered and asks for it back.
// The freelancer can still resign, or deliver and let the arbiter judge.
function requestRefund() external onlyClient
{
    _openCaseOf(CaseKind.RefundRequest);
}

function withdrawRequest() external
{
    Case memory openCase = _openCase;

    if (openCase.kind == CaseKind.None) {
        revert NoOpenCase();
    }

    address opener =
        openCase.kind == CaseKind.PaymentRequest
            ? freelancer
            : client;

    if (msg.sender != opener) {
        revert OnlyCaseOpener();
    }

    delete _openCase;

    emit CaseWithdrawn(
        nextMilestoneIndex,
        opener
    );
}

// The arbiter settles the open case. Only after the response window, so both
// sides have had time to settle it between themselves, and only between the
// client and the freelancer. freelancerAmount is what the freelancer gets
// from this milestone; the rest of it goes back to the client. With
// endEscrow the unreleased milestones after it also go back to the client and
// the escrow is over.
function settleCase(
    uint256 milestoneIndex,
    uint256 freelancerAmount,
    bool endEscrow
) external onlyArbiter nonReentrant
{
    Case memory openCase = _openCase;

    if (openCase.kind == CaseKind.None) {
        revert NoOpenCase();
    }

    uint256 availableAt =
        uint256(openCase.openedAt) + RESPONSE_WINDOW;

    if (block.timestamp < availableAt) {
        revert ResponseWindowNotElapsed(availableAt);
    }

    _settle(
        milestoneIndex,
        freelancerAmount,
        endEscrow
    );
}

// If the arbiter never acts, funds must not stay locked for ever. After
// STALL_WINDOW either side can split the milestone in half and end the
// escrow, handing everything unreleased after it back to the client.
function settleStalledCase() external nonReentrant
{
    if (msg.sender != client && msg.sender != freelancer) {
        revert OnlyParticipant();
    }

    Case memory openCase = _openCase;

    if (openCase.kind == CaseKind.None) {
        revert NoOpenCase();
    }

    uint256 availableAt =
        uint256(openCase.openedAt) + STALL_WINDOW;

    if (block.timestamp < availableAt) {
        revert StallWindowNotElapsed(availableAt);
    }

    uint256 index = nextMilestoneIndex;

    _settle(
        index,
        _milestones[index].amount / 2,
        true
    );
}

// The freelancer gives up the rest of the job. Everything unreleased goes
// back to the client; milestones already paid stay paid.
function resign() external onlyFreelancer nonReentrant
{
    if (escrowState != EscrowState.Funded) {
        revert InvalidEscrowState(
            EscrowState.Funded,
            escrowState
        );
    }

    if (!workStarted) {
        revert WorkNotStartedYet();
    }

    uint256 refundAmount =
        totalAmount - releasedAmount - refundedAmount;

    refundedAmount += refundAmount;

    delete _openCase;

    escrowState = EscrowState.Terminated;

    token.safeTransfer(
        client,
        refundAmount
    );

    emit FreelancerResigned(
        refundAmount
    );
}

function _openCaseOf(CaseKind kind) private
{
    if (escrowState != EscrowState.Funded) {
        revert InvalidEscrowState(
            EscrowState.Funded,
            escrowState
        );
    }

    if (!workStarted) {
        revert WorkNotStartedYet();
    }

    if (_openCase.kind != CaseKind.None) {
        revert CaseAlreadyOpen(_openCase.kind);
    }

    _openCase = Case({
        kind: kind,
        openedAt: uint64(block.timestamp)
    });

    emit CaseOpened(
        nextMilestoneIndex,
        kind,
        msg.sender,
        block.timestamp
    );
}

function _settle(
    uint256 milestoneIndex,
    uint256 freelancerAmount,
    bool endEscrow
) private
{
    if (escrowState != EscrowState.Funded) {
        revert InvalidEscrowState(
            EscrowState.Funded,
            escrowState
        );
    }

    if (milestoneIndex != nextMilestoneIndex) {
        revert CaseMilestoneMismatch(
            nextMilestoneIndex,
            milestoneIndex
        );
    }

    Milestone storage milestone =
        _milestones[milestoneIndex];

    if (freelancerAmount > milestone.amount) {
        revert FreelancerAmountTooLarge(
            milestone.amount,
            freelancerAmount
        );
    }

    uint256 clientAmount =
        milestone.amount - freelancerAmount;

    milestone.released = true;

    _paidToFreelancer[milestoneIndex] = freelancerAmount;

    _settledByCase[milestoneIndex] = true;

    releasedAmount += freelancerAmount;

    delete _openCase;

    nextMilestoneIndex++;

    bool completed =
        nextMilestoneIndex == _milestones.length;

    if (completed) {
        escrowState = EscrowState.Completed;
    } else if (endEscrow) {
        // Everything not yet paid out goes back to the client.
        clientAmount =
            totalAmount - releasedAmount - refundedAmount;

        escrowState = EscrowState.Terminated;
    }

    refundedAmount += clientAmount;

    if (freelancerAmount > 0) {
        _payFreelancer(milestoneIndex, freelancerAmount);
    }

    if (clientAmount > 0) {
        token.safeTransfer(
            client,
            clientAmount
        );
    }

    emit CaseSettled(
        milestoneIndex,
        freelancerAmount,
        clientAmount,
        endEscrow,
        msg.sender
    );

    if (completed) {
        emit EscrowCompleted(
            releasedAmount
        );
    }
}


    // V1 pays the full gross award. V2 overrides only this payment hook;
    // workflow, refund accounting and participant permissions stay shared.
    function _payFreelancer(uint256, uint256 grossAmount) internal virtual {
        token.safeTransfer(freelancer, grossAmount);
    }
}